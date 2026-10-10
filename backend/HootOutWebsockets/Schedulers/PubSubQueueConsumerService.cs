using HootOut.Contracts.Common.Messages.Services;
using HootOut.Contracts.WebSockets.Entities;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.Contracts.WebSockets.Services;

namespace HootOut.HootOutWebsockets.Schedulers
{
    public class PubSubQueueConsumerService : IHostedService
    {
        private IQueuePubService queuePubService;
        private IWSPubSubService wsPubSubService;

        public PubSubQueueConsumerService(IQueuePubService queuePubService, IWSPubSubService wSPubSubService)
        {
            this.queuePubService = queuePubService ?? throw new ArgumentNullException(nameof(queuePubService));
            this.wsPubSubService = wSPubSubService ?? throw new ArgumentNullException(nameof(wsPubSubService));
        }

        public Task StartAsync(CancellationToken ct)
        {
            return queuePubService.StartConsuming<WebSocketMessage>(nameof(WSHandlerType.ChatMessage),
                (exchange, channelId, message) => wsPubSubService.ReceiveMessageAsync(exchange, channelId, message),
                ct);
            ;
        }

        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
