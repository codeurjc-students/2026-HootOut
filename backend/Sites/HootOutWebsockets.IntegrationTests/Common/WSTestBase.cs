using Autofac;
using Dapper;
using FluentAssertions;
using HootOut.Authentication.Services;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.Services;
using HootOut.Contracts.Authentication.Services;
using HootOut.Contracts.ChatChannels.Requests;
using HootOut.Contracts.ChatChannels.Services;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Contracts.ChatServers.Requests;
using HootOut.Contracts.ChatServers.Services;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Enums;
using HootOut.Contracts.Common.Presence;
using HootOut.Contracts.Friends.Requests;
using HootOut.Contracts.Friends.Services;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.Contracts.Users.Services;
using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.Contracts.WebSockets.Services;
using HootOut.HootOutWebsockets.Contracts;
using HootOut.HootOutWebsockets.Presence;
using HootOut.HootOutWebsockets.Services;
using HootOut.RabbitMQ.Configuration;
using HootOut.Redis.Configuration;
using HootOut.WebSockets.Service;
using Newtonsoft.Json;

namespace HootOut.HootOutWebsockets.IntegrationTests.Common
{
    public sealed record TestUser(Guid Id, string Username);

    /// <summary>
    /// Common setup for the WebSocket integration tests: same registration as WSChatMessagesTests,
    /// plus helpers to create users, friends, servers and to send and receive chat messages.
    /// </summary>
    public abstract class WSTestBase : IAsyncLifetime, IDisposable
    {
        protected static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        // Errors sent back by the server (same text as the constants in the handlers and ChatMessageService)
        protected const string CannotSendMessageError = "Cannot send a message to this conversation";
        protected const string CannotSubscribeError = "Cannot subscribe to this conversation";
        protected const string MissingChannelError = "Channel is required";
        protected const string InvalidChannelError = "Invalid channel";
        protected const string InvalidPayloadError = "Invalid message";
        protected const string UnsupportedTypeError = "Unsupported message type";
        protected const string CannotSubscribePresenceError = "Cannot subscribe to this server";

        protected Guid GetConnectionId(Guid userId)
            => connectionManager.Connections.Single(x => x.Value.User?.UserId == userId).Key;

        protected readonly WebSocketServerFixture serverFixture;
        protected readonly ILifetimeScope container;
        protected readonly ClearAllTables clearTables;

        protected readonly IPersistenceProvider persistenceProvider;
        protected readonly IUserService userService;
        protected readonly IAuthenticationService authService;
        protected readonly IFriendsService friendsService;
        protected readonly IChatServerService serverService;
        protected readonly IChatChannelsService channelService;
        protected readonly IChatMessageService chatMessageService;

        protected readonly WSConnectionManager connectionManager;
        protected readonly WSPubSubService pubSubService;
        protected readonly IPresenceStore presenceStore;

        protected WSTestBase(WebSocketServerFixture serverFixture, TestContaineRabbitConnectionFactory rabbitConnectionFactory)
        {
            this.serverFixture = serverFixture;
            var rsaKeyProvider = new DevelopmentRsaKeyProvider("keys/jwt-private.pem");

            this.serverFixture.RegisterDependencies = (ContainerBuilder? builder) =>
            {
                builder?.RegisterInstance(rsaKeyProvider).As<IRsaKeyProvider>().SingleInstance();
                builder?.RegisterInstance(rabbitConnectionFactory).As<IRabbitConnectionFactory>().SingleInstance();
            };

            container = serverFixture.AutofacRoot.BeginLifetimeScope();

            persistenceProvider = container.Resolve<IPersistenceProvider>();
            Assert.Same(serverFixture.PostgreSQLProvider, persistenceProvider);
            Assert.Same(rabbitConnectionFactory, container.Resolve<IRabbitConnectionFactory>());
            Assert.Same(serverFixture.RedisConnectionFactory, container.Resolve<IRedisConnectionFactory>());

            clearTables = container.Resolve<ClearAllTables>();
            var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority);

            foreach (var value in defaultValues)
            {
                value.InitAsync().Wait();
            }

            userService = container.Resolve<IUserService>();
            authService = container.Resolve<IAuthenticationService>();
            friendsService = container.Resolve<IFriendsService>();
            serverService = container.Resolve<IChatServerService>();
            channelService = container.Resolve<IChatChannelsService>();
            chatMessageService = container.Resolve<IChatMessageService>();

            // Singletons of the WebSocket server: shared by every test of the class
            connectionManager = (WSConnectionManager)container.Resolve<IWSConnectionManager>();
            pubSubService = (WSPubSubService)container.Resolve<IWSPubSubService>();
            presenceStore = container.Resolve<IPresenceStore>();
        }

        public async ValueTask InitializeAsync()
        {
            await clearTables.ClearTablesAsync();

            // The previous test of the class must have released all its connections
            await WaitUntilAsync(() => connectionManager.Connections.IsEmpty && pubSubService.ConnectionSubscriptions.IsEmpty);
        }

        public async ValueTask DisposeAsync()
        {
            await clearTables.ClearTablesAsync();
        }

        public void Dispose()
        {
            container.Dispose();
            GC.SuppressFinalize(this);
        }

        #region Users and connections

        protected async Task<TestUser> CreateUserAsync(string username)
        {
            var user = await userService.CreateUserAsync(new CreateUserRequest
            {
                Username = username,
                Email = $"{username}@test.com",
                Password = "test123"
            });

            return new TestUser(user.Id, username);
        }

        protected async Task<List<TestUser>> CreateUsersAsync(int count, string prefix = "wsuser")
        {
            var users = new List<TestUser>(count);
            for (var i = 1; i <= count; i++)
            {
                users.Add(await CreateUserAsync($"{prefix}{i}"));
            }
            return users;
        }

        /// <summary>
        /// Gets a new ticket, connects and waits until the server has registered the connection.
        /// Presence messages are ignored: use <see cref="ConnectWithPresenceAsync"/> to receive them.
        /// </summary>
        protected async Task<WSTestClient> ConnectAsync(TestUser user, CancellationToken ct)
        {
            var ticket = await authService.CreateWebSocketTicketAsync(user.Id, user.Username, ct);
            var before = CountConnections(user.Id);

            var client = await WSTestClient.ConnectAsync(serverFixture, wsTicket: ticket.Ticket, ct: ct);
            await WaitUntilAsync(() => CountConnections(user.Id) > before);

            return client;
        }

        /// <summary>
        /// Connects receiving presence messages. Returns after the friends snapshot arrived and the user is online
        /// in the store, so the Online of this user has already been published.
        /// </summary>
        protected async Task<(WSTestClient Client, PresenceSnapshotPayload Snapshot)> ConnectWithPresenceAsync(TestUser user, CancellationToken ct)
        {
            var client = await ConnectAsync(user, ct);
            client.IgnorePresence = false;

            var snapshot = await client.ReceiveAsync(ct);
            snapshot.Type.Should().Be(WSHandlerType.PresenceSnapshot);
            snapshot.Channel.Should().Be(PresenceTracker.FriendsSnapshotChannel);

            await WaitUntilOnlineAsync(user, true);
            return (client, ReadPayload<PresenceSnapshotPayload>(snapshot));
        }

        protected Task WaitUntilOnlineAsync(TestUser user, bool online, TimeSpan? timeout = null)
            => WaitUntilAsync(() => presenceStore.IsOnlineAsync(user.Id).GetAwaiter().GetResult() == online, timeout);

        /// <summary>Subscriptions of the user's connection that are not presence channels.</summary>
        protected IEnumerable<string> ChatSubscriptions(Guid userId)
            => pubSubService.ConnectionSubscriptions.TryGetValue(GetConnectionId(userId), out var channels)
                ? channels.Where(x => !x.StartsWith("presence.")).ToList()
                : [];

        protected int CountConnections(Guid userId)
            => connectionManager.Connections.Values.Count(x => x.User?.UserId == userId);

        protected int CountSubscribers(Guid conversationId)
            => pubSubService.ChannelSubscribers.TryGetValue(conversationId.ToString(), out var subscribers) ? subscribers.Count : 0;

        protected static async Task WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null)
        {
            var limit = DateTime.UtcNow + (timeout ?? WaitTimeout);
            while (!condition())
            {
                if (DateTime.UtcNow > limit)
                    throw new TimeoutException("The condition was not met in time");
                await Task.Delay(20);
            }
        }

        #endregion

        #region WebSocket messages

        protected static Task<WebSocketMessageDto> SubscribeAsync(WSTestClient client, Guid conversationId, CancellationToken ct)
            => client.SendWithAckAsync(new WebSocketMessageDto
            {
                Type = WSHandlerType.Subscribe,
                Channel = conversationId.ToString()
            }, ct);

        protected static Task<WebSocketMessageDto> UnsubscribeAsync(WSTestClient client, Guid conversationId, CancellationToken ct)
            => client.SendWithAckAsync(new WebSocketMessageDto
            {
                Type = WSHandlerType.Unsubscribe,
                Channel = conversationId.ToString()
            }, ct);

        protected static WebSocketMessageDto ChatMessage(Guid conversationId, string content, Guid? id = null)
            => ChatMessage(conversationId, new CreateChatMessageRequest { Content = content }, id);

        protected static WebSocketMessageDto ChatMessage(Guid conversationId, CreateChatMessageRequest request, Guid? id = null)
            => new()
            {
                Id = id,
                Type = WSHandlerType.ChatMessage,
                Channel = conversationId.ToString(),
                Payload = JsonConvert.SerializeObject(request)
            };

        /// <summary>Sends a chat message, waits for its Ack and returns the saved message that comes in the Ack.</summary>
        protected static async Task<ChatMessageDto> SendChatMessageAsync(WSTestClient client, Guid conversationId, string content, CancellationToken ct)
        {
            var message = ChatMessage(conversationId, content, Guid.NewGuid());
            var ack = await client.SendWithAckAsync(message, ct);

            ack.Type.Should().Be(WSHandlerType.Ack);
            ack.ReplyTo.Should().Be(message.Id);
            ack.Channel.Should().Be(conversationId.ToString());
            ack.Error.Should().BeNull();

            var saved = ReadChatMessage(ack);
            saved.Content.Should().Be(content);
            saved.ChatConversationId.Should().Be(conversationId);
            return saved;
        }

        /// <summary>Sends a message that the server has to reject and returns the error text.</summary>
        protected static async Task<string?> SendExpectingErrorAsync(WSTestClient client, WebSocketMessageDto message, CancellationToken ct)
        {
            var reply = await client.SendAndGetReplyAsync(message, ct);

            reply.Type.Should().Be(WSHandlerType.Error);
            reply.ReplyTo.Should().Be(message.Id);
            reply.Error.Should().NotBeNullOrWhiteSpace();
            return reply.Error;
        }

        protected static ChatMessageDto ReadChatMessage(WebSocketMessageDto message)
        {
            message.Payload.Should().NotBeNullOrWhiteSpace();
            var chatMessage = JsonConvert.DeserializeObject<ChatMessageDto>(message.Payload!);
            chatMessage.Should().NotBeNull();
            return chatMessage!;
        }

        /// <summary>The next message must be a broadcast chat message with this content and author.</summary>
        protected static async Task<ChatMessageDto> ExpectChatMessageAsync(WSTestClient client, Guid conversationId, string content, TestUser author, CancellationToken ct)
        {
            var received = await client.ReceiveAsync(ct);

            received.Type.Should().Be(WSHandlerType.ChatMessage);
            received.ReplyTo.Should().BeNull();
            received.Channel.Should().Be(conversationId.ToString());

            var chatMessage = ReadChatMessage(received);
            chatMessage.Content.Should().Be(content);
            chatMessage.ChatConversationId.Should().Be(conversationId);
            chatMessage.Author.Should().NotBeNull();
            chatMessage.Author!.Id.Should().Be(author.Id);
            chatMessage.Author.Username.Should().Be(author.Username);
            return chatMessage;
        }

        protected async Task<List<ChatMessageDto>> GetMessagesOldestFirstAsync(Guid conversationId, CancellationToken ct)
        {
            var page = await chatMessageService.GetMessagesByConversationIdAsync(conversationId, next: null, pageSize: 100, ct: ct);
            return Enumerable.Reverse(page.Items).ToList(); // The repository returns the newest first
        }

        #endregion

        #region Presence

        protected static T ReadPayload<T>(WebSocketMessageDto message)
        {
            message.Payload.Should().NotBeNullOrWhiteSpace();
            var payload = JsonConvert.DeserializeObject<T>(message.Payload!);
            payload.Should().NotBeNull();
            return payload!;
        }

        /// <summary>Sends PresenceSubscribe and returns the snapshot of the server, which arrives before the Ack.</summary>
        protected static async Task<PresenceSnapshotPayload> PresenceSubscribeAsync(WSTestClient client, Guid serverId, CancellationToken ct)
        {
            var ack = await client.SendWithAckAsync(new WebSocketMessageDto
            {
                Type = WSHandlerType.PresenceSubscribe,
                Channel = serverId.ToString()
            }, ct);
            ack.Type.Should().Be(WSHandlerType.Ack);

            var snapshot = await client.ReceiveUntilAsync(
                x => x.Type == WSHandlerType.PresenceSnapshot && x.Channel == serverId.ToString(), ct);
            return ReadPayload<PresenceSnapshotPayload>(snapshot);
        }

        protected static Task<WebSocketMessageDto> PresenceUnsubscribeAsync(WSTestClient client, Guid serverId, CancellationToken ct)
            => client.SendWithAckAsync(new WebSocketMessageDto
            {
                Type = WSHandlerType.PresenceUnsubscribe,
                Channel = serverId.ToString()
            }, ct);

        /// <summary>The next message must be a presence change of <paramref name="user"/> on <paramref name="channel"/>.</summary>
        protected static async Task ExpectPresenceAsync(WSTestClient client, string channel, TestUser user, PresenceStatus status, CancellationToken ct)
        {
            var received = await client.ReceiveAsync(ct);

            received.Type.Should().Be(WSHandlerType.Presence);
            received.ReplyTo.Should().BeNull();
            received.Channel.Should().Be(channel);

            var payload = ReadPayload<PresenceChangedPayload>(received);
            payload.UserId.Should().Be(user.Id);
            payload.Status.Should().Be(status);
        }

        protected static string FriendChannel(TestUser user) => PresenceTracker.UserChannel(user.Id);

        protected static string ServerChannel(Guid serverId) => PresenceTracker.ServerChannel(serverId);

        protected int CountPresenceSubscribers(string channel)
            => pubSubService.ChannelSubscribers.TryGetValue(channel, out var subscribers) ? subscribers.Count : 0;

        #endregion

        #region Friends

        /// <summary>The first user sends the request and the second one accepts it.</summary>
        protected async Task<(Guid RelationId, Guid ConversationId)> MakeFriendsAsync(TestUser user, TestUser friend, CancellationToken ct)
        {
            var request = await friendsService.SendFriendRequestAsync(new SendFriendRequest { Username = friend.Username }, user.Id, ct);

            var relationId = await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = request.Id,
                Status = InvitationStatus.Accepted
            }, friend.Id, ct);
            relationId.Should().NotBeNull();

            var friends = await friendsService.GetUserFriendsAsync(new PageRequest { PageSize = 50 }, user.Id, ct);
            var conversationId = friends.Items.Single(x => x.Friend.Id == friend.Id).ChatConversationId;

            return (relationId!.Value, conversationId);
        }

        protected async Task<Guid> SendFriendRequestAsync(TestUser user, TestUser invited, CancellationToken ct)
            => (await friendsService.SendFriendRequestAsync(new SendFriendRequest { Username = invited.Username }, user.Id, ct)).Id;

        /// <summary>There is no endpoint to remove a friend yet, so the relation is marked as deleted with SQL.</summary>
        protected async Task DeleteFriendRelationAsync(Guid relationId, CancellationToken ct)
        {
            await using var conn = await persistenceProvider.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                """UPDATE "HootOut"."FriendRelation" SET "Deleted" = true WHERE "Id" = @Id""",
                new { Id = relationId }, cancellationToken: ct));
        }

        #endregion

        #region Servers

        /// <summary>Creates a server owned by the user with one channel. Returns the channel conversation.</summary>
        protected async Task<(Guid ServerId, Guid ConversationId)> CreateServerWithChannelAsync(TestUser owner, CancellationToken ct, string name = "test server")
        {
            var server = await serverService.CreateChatServerAsync(new CreateChatServerRequest { Name = name }, owner.Id, ct);
            var conversationId = await CreateChannelAsync(server.Id, ct);
            return (server.Id, conversationId);
        }

        protected async Task<Guid> CreateChannelAsync(Guid serverId, CancellationToken ct, string name = "general")
        {
            var channel = await channelService.CreateChatChannelAsync(new CreateChatChannelRequest
            {
                ChatServerId = serverId,
                Name = name
            }, ct);
            return channel.ChatConversationId;
        }

        protected async Task<Guid> InviteAsync(Guid serverId, TestUser inviter, TestUser invited, CancellationToken ct)
        {
            var invitation = await serverService.InviteMemberAsync(new InviteServerMemberRequest
            {
                ChatServerId = serverId,
                Username = invited.Username
            }, inviter.Id, ct);
            return invitation.Id;
        }

        protected Task UpdateInvitationAsync(Guid invitationId, InvitationStatus status, TestUser user, CancellationToken ct)
            => serverService.UpdateServerInvitationAsync(new UpdateServerInvitationRequest
            {
                Id = invitationId,
                Status = status
            }, user.Id, ct);

        protected async Task AddMemberAsync(Guid serverId, TestUser owner, TestUser member, CancellationToken ct)
        {
            var invitationId = await InviteAsync(serverId, owner, member, ct);
            await UpdateInvitationAsync(invitationId, InvitationStatus.Accepted, member, ct);
        }

        /// <summary>There is no endpoint to remove a member yet, so the member is marked as deleted with SQL.</summary>
        protected async Task RemoveMemberAsync(Guid serverId, TestUser member, CancellationToken ct)
        {
            await using var conn = await persistenceProvider.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                """UPDATE "HootOut"."ServerMember" SET "Deleted" = true WHERE "ChatServerId" = @ServerId AND "UserId" = @UserId""",
                new { ServerId = serverId, UserId = member.Id }, cancellationToken: ct));
        }

        #endregion
    }
}
