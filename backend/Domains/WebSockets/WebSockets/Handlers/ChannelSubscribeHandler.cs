using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Entities;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.Contracts.WebSockets.Services;
using Microsoft.Extensions.Logging;

namespace HootOut.WebSockets.Handlers
{
    public class ChannelSubscribeHandler : IWSMessageHandler
    {
        public WSHandlerType Type => WSHandlerType.Subscribe;

        private readonly ILogger<ChannelSubscribeHandler> logger;

        private readonly IWSPubSubService wsPubSubService;

        public ChannelSubscribeHandler(
            ILogger<ChannelSubscribeHandler> logger,
            IWSPubSubService wsPubSubService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.wsPubSubService = wsPubSubService ?? throw new ArgumentNullException(nameof(wsPubSubService));
        }

        public async Task HandleMessageAsync(Guid connectionId, WSUserInfo user, WebSocketMessage message, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(message.Channel))
            {
                logger.LogWarning("Empty subscription channel for {ConnectionId} for user {UserId}", connectionId, user);
                return;
            }

            await wsPubSubService.HandleSubscribeAsync(connectionId, nameof(WSHandlerType.ChatMessage), message.Channel, ct); //TO-DO Hardcoded subscribe to ChatMessage
        }
    }
}
