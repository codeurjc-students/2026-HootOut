using FluentValidation;
using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Entities;
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

        public async Task HandleMessageAsync(Guid connectionId, WSUserInfo user, WebSocketMessage message, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(message.Channel))
            {
                logger.LogWarning("Empty subscription channel for {ConnectionId} for user {UserId}", connectionId, user.UserId);
                throw new ValidationException("Channel is required");
            }

            // No permission check: unsubscribing from a channel the connection is not subscribed to does nothing
            await wsPubSubService.HandleUnsubscribeAsync(connectionId, nameof(WSHandlerType.ChatMessage), message.Channel, ct); //TO-DO Hardcoded unsubscribe ChatMessage
        }
    }
}
