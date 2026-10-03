using HootOut.Contracts.WebSocket;
using HootOut.Contracts.WebSockets.Services;
using HootOut.HootOutWebsockets.Services;
using System.Collections.Concurrent;

namespace HootOut.WebSockets.Service
{
    public class WSPubSubService : IWSPubSubService
    {
        private readonly ILogger<WSPubSubService> logger;
        private readonly IWSConnectionManager wsConnectionManager;
        public readonly ConcurrentDictionary<string, HashSet<Guid>> ChannelSubscribers = new();

        public readonly ConcurrentDictionary<Guid, HashSet<string>> ConnectionSubscriptions = new();

        public WSPubSubService(ILogger<WSPubSubService> logger, IWSConnectionManager wsConnectionManager)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.wsConnectionManager = wsConnectionManager ?? throw new ArgumentNullException(nameof(wsConnectionManager));
        }

        public async Task HandleSubscribeAsync(Guid connectionId, string userId, string channel)
        {
            //TO-DO Subscribe to rabbit queue
            ChannelSubscribers.AddOrUpdate(channel, new HashSet<Guid> { connectionId }, (_, existing) =>
            {
                lock (existing)
                {
                    existing.Add(connectionId);
                    logger.LogInformation("Added connection {ConnectionId} to channel {ChannelId}. Channel count {ConnectionCount}", connectionId, channel, existing.Count);
                }
                return existing;
            });

            ConnectionSubscriptions.AddOrUpdate(connectionId, new HashSet<string> { channel }, (_, existing) =>
            {
                lock (existing)
                {
                    existing.Add(channel);
                }
                return existing;
            });
        }

        public async Task HandleUnsubscribeAsync(Guid connectionId, string userId, string channel)
        {
            //TO-DO if no subscribers unsubscribe from rabbitq
            if (ChannelSubscribers.TryGetValue(channel, out var subscribers))
            {
                lock (subscribers)
                {
                    subscribers.Remove(connectionId);
                    logger.LogInformation("Removed connection {ConnectionId} from channel {ChannelId}. Channel count {ConnectionCount}", connectionId, channel, subscribers.Count);
                    if (subscribers.Count == 0)
                    {
                        ChannelSubscribers.TryRemove(channel, out _);
                    }
                }
            }

            if (ConnectionSubscriptions.TryGetValue(connectionId, out var channels))
            {
                lock (channels)
                {
                    channels.Remove(channel);
                    logger.LogInformation("Removed connection {ConnectionId} from channel {ChannelId}. Channel count {ConnectionCount}", connectionId, channel, subscribers.Count);
                    if (channels.Count == 0)
                    {
                        ConnectionSubscriptions.TryRemove(connectionId, out _);
                    }
                }
            }
        }

        public async Task RemoveAllConnectionSubscriptions(Guid connectionId)
        {
            if (ConnectionSubscriptions.TryGetValue(connectionId, out var channels))
            {
                foreach (var channel in channels)
                {
                    if (ChannelSubscribers.TryGetValue(channel, out var subscribers))
                    {
                        lock (subscribers)
                        {
                            subscribers.Remove(connectionId);
                            logger.LogInformation("Removed connection {ConnectionId} from channel {ChannelId}. Channel count {ConnectionCount}", connectionId, channel, subscribers.Count);
                            if (subscribers.Count == 0)
                            {
                                ChannelSubscribers.TryRemove(channel, out _);
                            }
                        }
                    }
                }
            }

            ConnectionSubscriptions.TryRemove(connectionId, out _);

        }

        public async Task SendMessageToChannel(Guid connectionId, string channel, WebSocketMessage message)
        {
            // TO-DO Send message to rabbit queue
            if (ChannelSubscribers.TryGetValue(channel, out var subscribers))
            {
                IEnumerable<Guid> currentSubscribers = subscribers.Where(x => !connectionId.Equals(x)).ToList();
                foreach (var subscriber in currentSubscribers)
                {
                    try
                    {
                        await wsConnectionManager.SendMessageAsync(subscriber, message);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Error sending message to Websocket {ConnectionId}", subscriber);
                    }
                }
            }
        }
    }
}
