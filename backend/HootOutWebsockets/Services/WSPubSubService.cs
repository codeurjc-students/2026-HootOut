using HootOut.Contracts.Common.Messages.Services;
using HootOut.Contracts.WebSockets.Entities;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.Contracts.WebSockets.Mappers;
using HootOut.Contracts.WebSockets.Services;
using HootOut.HootOutWebsockets.Contracts;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Text;

namespace HootOut.WebSockets.Service
{
    public class WSPubSubService : IWSPubSubService
    {
        private readonly ILogger<WSPubSubService> logger;
        private readonly IWSConnectionManager wsConnectionManager;
        private readonly IQueuePubService queuePubService;
        public readonly ConcurrentDictionary<string, HashSet<Guid>> ChannelSubscribers = new();
        public readonly ConcurrentDictionary<Guid, HashSet<string>> ConnectionSubscriptions = new();

        private readonly SemaphoreSlim initLock = new(1, 1);

        public WSPubSubService(ILogger<WSPubSubService> logger, IWSConnectionManager wsConnectionManager, IQueuePubService queuePubService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.wsConnectionManager = wsConnectionManager ?? throw new ArgumentNullException(nameof(wsConnectionManager));
            this.queuePubService = queuePubService ?? throw new ArgumentNullException(nameof(queuePubService));
        }

        public async Task HandleSubscribeAsync(Guid connectionId, string exchange, string channel, CancellationToken ct = default)
        {
            await initLock.WaitAsync(ct);
            try
            {
                var subscribers = ChannelSubscribers.GetOrAdd(channel, _ => new HashSet<Guid>());
                if (subscribers.Count == 0)
                {
                    await queuePubService.Subscribe(exchange, channel, ct); //messageQueue subscription
                }
                subscribers.Add(connectionId);

                ConnectionSubscriptions.AddOrUpdate(connectionId, new HashSet<string> { channel }, (_, existing) =>
                {
                    existing.Add(channel);
                    return existing;
                });
            }
            finally
            {
                initLock.Release();
            }
        }

        public async Task HandleUnsubscribeAsync(Guid connectionId, string exchange, string channel, CancellationToken ct)
        {
            await initLock.WaitAsync(ct);
            try
            {
                if (!ChannelSubscribers.TryGetValue(channel, out var subscribers))
                    return;

                subscribers.Remove(connectionId);
                logger.LogInformation("Removed connection {ConnectionId} from channel {ChannelId}. Channel count {ConnectionCount}", connectionId, channel, subscribers.Count);
                if (subscribers.Count == 0)
                {
                    ChannelSubscribers.TryRemove(channel, out _);
                    await queuePubService.UnSubscribe(exchange, channel, ct);
                }


                if (!ConnectionSubscriptions.TryGetValue(connectionId, out var channels))
                    return;

                channels.Remove(channel);

                if (channels.Count == 0)
                {
                    ConnectionSubscriptions.TryRemove(connectionId, out _);
                }
            }
            finally
            {
                initLock.Release();
            }
        }

        public async Task RemoveAllConnectionSubscriptions(Guid connectionId, CancellationToken ct)
        {
            if (!ConnectionSubscriptions.TryGetValue(connectionId, out var channels))
                return;

            foreach (var channel in channels)
            {
                await HandleUnsubscribeAsync(connectionId, nameof(WSHandlerType.ChatMessage), channel, ct); //TO-DO hardcoded exchange chat message
            }
        }

        public async Task SendMessageAsync(string exchange, string channel, WebSocketMessage message, CancellationToken ct)
        {
            await this.queuePubService.Publish(exchange, channel, message, ct);
        }

        public async Task ReceiveMessageAsync(string exchange, string channel, WebSocketMessage message)
        {
            if (!ChannelSubscribers.TryGetValue(channel, out var subscribers))
                return;

            var serializedMessage = JsonConvert.SerializeObject(message.ToDto()); //DTO removes server data, like the original connectionId
            var rawMessage = Encoding.UTF8.GetBytes(serializedMessage);

            IEnumerable<Guid> currentSubscribers = subscribers.Where(x => x != message.ConnectionId).ToList();
            foreach (var subscriber in currentSubscribers)
            {
                try
                {
                    await wsConnectionManager.SendMessageAsync(subscriber, rawMessage);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error sending message to Websocket {ConnectionId}", subscriber);
                    throw;
                }
            }
        }
    }
}
