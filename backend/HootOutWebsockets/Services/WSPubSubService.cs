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
        private readonly ConcurrentDictionary<string, HashSet<string>> channelSubscribers = new();

        public WSPubSubService(ILogger<WSPubSubService> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task HandleSubscribeAsync(string connectionId, string userId, string channel)
        {
            //TO-DO Subscribe to rabbit queue
            channelSubscribers.AddOrUpdate(channel, new HashSet<string> { connectionId }, (_, existing) =>
            {
                lock (existing)
                {
                    existing.Add(connectionId);
                }
                return existing;
            });
        }

        public async Task HandleUnsubscribeAsync(string connectionId, string userId, string channel)
        {
            //TO-DO if no subscribers unsubscribe from rabbitq
            if (channelSubscribers.TryGetValue(channel, out var subscribers))
            {
                lock (subscribers)
                {
                    subscribers.Remove(connectionId);
                    if (subscribers.Count == 0)
                    {
                        channelSubscribers.TryRemove(channel, out _);
                    }
                }
            }
        }

        public async Task SendMessageToChannel(string channel, WebSocketMessage message)
        {
            // TO-DO Send message to rabbit queue
            if (channelSubscribers.TryGetValue(channel, out var subscribers))
            {
                IEnumerable<string> currentSubscribers = subscribers.ToList();
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
