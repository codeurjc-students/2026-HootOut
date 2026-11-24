using HootOut.Contracts.ChatServers.Services;
using HootOut.Contracts.Common.Presence;
using HootOut.Contracts.Friends.Services;
using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Entities;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.Contracts.WebSockets.Services;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace HootOut.HootOutWebsockets.Presence
{
    /// <summary>
    /// Keeps the presence store in sync with the connections of this instance and publishes the transitions
    /// through RabbitMQ, exchange "Presence":
    /// <list type="bullet">
    /// <item><c>presence.user.&lt;userId&gt;</c>: every connection is bound to the channels of its friends on connect.</item>
    /// <item><c>presence.server.&lt;serverId&gt;</c>: bound with PresenceSubscribe while the client shows that server.</item>
    /// </list>
    /// An instance only receives the changes its own clients are interested in. Singleton.
    /// </summary>
    public sealed class PresenceTracker : IPresenceTracker
    {
        public const string Exchange = nameof(WSHandlerType.Presence);
        public const string FriendsSnapshotChannel = "friends";

        public static string UserChannel(Guid userId) => $"presence.user.{userId}";
        public static string ServerChannel(Guid serverId) => $"presence.server.{serverId}";

        private readonly ILogger<PresenceTracker> logger;
        private readonly IPresenceStore store;
        private readonly IWSPubSubService pubSubService;
        private readonly IWSMessageSender messageSender;
        private readonly IFriendsService friendsService;
        private readonly IChatServerService chatServerService;
        private readonly PresenceOptions options;

        // Local connections of this instance: connectionId -> userId. Used to add them again after a resync.
        private readonly ConcurrentDictionary<Guid, Guid> localConnections = new();

        private readonly CancellationTokenSource shutdown = new();

        public PresenceTracker(
            ILogger<PresenceTracker> logger,
            IPresenceStore store,
            IWSPubSubService pubSubService,
            IWSMessageSender messageSender,
            IFriendsService friendsService,
            IChatServerService chatServerService,
            PresenceOptions options)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.pubSubService = pubSubService ?? throw new ArgumentNullException(nameof(pubSubService));
            this.messageSender = messageSender ?? throw new ArgumentNullException(nameof(messageSender));
            this.friendsService = friendsService ?? throw new ArgumentNullException(nameof(friendsService));
            this.chatServerService = chatServerService ?? throw new ArgumentNullException(nameof(chatServerService));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task ConnectedAsync(WSUserInfo user, Guid connectionId, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(user);

            localConnections[connectionId] = user.UserId;

            // 1. Subscribe before reading the snapshot: a change that happens in between is not lost
            var friendIds = await friendsService.GetFriendIdsAsync(user.UserId, ct);
            foreach (var friendId in friendIds)
            {
                await pubSubService.HandleSubscribeAsync(connectionId, Exchange, UserChannel(friendId), ct);
            }

            // 2. Who is online right now
            await SendSnapshotAsync(connectionId, FriendsSnapshotChannel, friendIds, ct);

            // 3. Announce this user if it is its first connection
            if (await store.AddConnectionAsync(user.UserId, connectionId, ct))
            {
                // Can follow a disconnect still inside its grace period: friends then get a second "online",
                // which clients treat as a no-op
                await PublishAsync(user.UserId, PresenceStatus.Online, ct);
            }
        }

        public async Task DisconnectedAsync(WSUserInfo user, Guid connectionId, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(user);

            localConnections.TryRemove(connectionId, out _);

            if (!await store.RemoveConnectionAsync(user.UserId, connectionId, ct))
                return;

            if (shutdown.IsCancellationRequested)
            {
                // The process is stopping: the delayed check would never run
                await PublishAsync(user.UserId, PresenceStatus.Offline, CancellationToken.None);
                return;
            }

            _ = PublishOfflineAfterGraceAsync(user.UserId);
        }

        public async Task SubscribeServerAsync(WSUserInfo user, Guid connectionId, Guid serverId, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(user);

            // Throws ValidationException if the user is not a member of the server
            var memberIds = await chatServerService.GetServerMemberIdsAsync(serverId, user.UserId, ct);

            await pubSubService.HandleSubscribeAsync(connectionId, Exchange, ServerChannel(serverId), ct);
            await SendSnapshotAsync(connectionId, serverId.ToString(), memberIds, ct);
        }

        public Task UnsubscribeServerAsync(Guid connectionId, Guid serverId, CancellationToken ct = default)
        {
            return pubSubService.HandleUnsubscribeAsync(connectionId, Exchange, ServerChannel(serverId), ct);
        }

        /// <summary>Publishes the change to the friends of the user and to every server the user is a member of.</summary>
        public async Task PublishAsync(Guid userId, PresenceStatus status, CancellationToken ct = default)
        {
            var payload = JsonConvert.SerializeObject(new PresenceChangedPayload { UserId = userId, Status = status });

            // Only on transitions (rare), so one query per publish is fine
            var serverIds = await chatServerService.GetServerIdsByUserIdAsync(userId, ct);

            var channels = serverIds.Select(ServerChannel).Prepend(UserChannel(userId));
            foreach (var channel in channels)
            {
                await pubSubService.SendMessageAsync(Exchange, channel, new WebSocketMessage
                {
                    Type = WSHandlerType.Presence,
                    Channel = channel,
                    Payload = payload,
                }, ct);
            }
        }

        /// <summary>Adds all the local connections again. Used when the "alive" key of the instance had expired.</summary>
        public async Task ResyncAsync(CancellationToken ct)
        {
            foreach (var (connectionId, userId) in localConnections.ToArray())
            {
                if (await store.AddConnectionAsync(userId, connectionId, ct))
                {
                    await PublishAsync(userId, PresenceStatus.Online, ct);
                }
            }
        }

        /// <summary>Called when the host stops: from now on offline is published without grace period.</summary>
        public void BeginShutdown() => shutdown.Cancel();

        private async Task SendSnapshotAsync(Guid connectionId, string channel, IEnumerable<Guid> userIds, CancellationToken ct)
        {
            var online = await store.GetOnlineUsersAsync(userIds, ct);
            await messageSender.SendMessageAsync(connectionId, new WebSocketMessageDto
            {
                Type = WSHandlerType.PresenceSnapshot,
                Channel = channel,
                Payload = JsonConvert.SerializeObject(new PresenceSnapshotPayload { OnlineUserIds = online.ToList() }),
            });
        }

        private async Task PublishOfflineAfterGraceAsync(Guid userId)
        {
            try
            {
                await Task.Delay(options.OfflineGracePeriod, shutdown.Token);
            }
            catch (OperationCanceledException)
            {
                // Shutdown during the grace period: publish now
            }

            try
            {
                // Reconnected (here or in another instance) during the grace period: nothing to announce
                if (await store.IsOnlineAsync(userId))
                    return;

                await PublishAsync(userId, PresenceStatus.Offline);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error publishing offline for user {UserId}", userId);
            }
        }
    }
}
