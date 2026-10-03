using HootOut.Contracts.WebSocket;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.Contracts.WebSockets.Services;
using Microsoft.Extensions.Logging;

namespace HootOut.WebSockets.Handlers
{
    public class ChannelUnsubscribeHandler : IWSMessageHandler
    {
        public WSHandlerType Type => WSHandlerType.Unsubscribe;

        private readonly ILogger<ChannelUnsubscribeHandler> logger;
        private readonly IWSPubSubService wsPubSubService;

        public ChannelUnsubscribeHandler(
            ILogger<ChannelUnsubscribeHandler> logger,
            IWSPubSubService wsPubSubService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.wsPubSubService = wsPubSubService ?? throw new ArgumentNullException(nameof(wsPubSubService));
        }

        public async Task HandleMessageAsync(Guid connectionId, string userId, WebSocketMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.Channel))
            {
                logger.LogWarning("Empty subscription channel for {ConnectionId} for user {UserId}", connectionId, userId);
                return;
            }

            await wsPubSubService.HandleUnsubscribeAsync(connectionId, userId, message.Channel);
        }
    }
}
