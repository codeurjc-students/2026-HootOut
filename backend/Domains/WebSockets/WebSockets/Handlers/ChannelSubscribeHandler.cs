using FluentValidation;
using HootOut.Contracts.ChatMessage.Services;
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

        private readonly IChatMessageService chatMessageService;

        public ChannelSubscribeHandler(
            ILogger<ChannelSubscribeHandler> logger,
            IWSPubSubService wsPubSubService,
            IChatMessageService chatMessageService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.wsPubSubService = wsPubSubService ?? throw new ArgumentNullException(nameof(wsPubSubService));
            this.chatMessageService = chatMessageService ?? throw new ArgumentNullException(nameof(chatMessageService));
        }

        public async Task HandleMessageAsync(Guid connectionId, WSUserInfo user, WebSocketMessage message, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(message.Channel))
            {
                logger.LogWarning("Empty subscription channel for {ConnectionId} for user {UserId}", connectionId, user.UserId);
                throw new ValidationException("Channel is required");
            }

            if (!Guid.TryParse(message.Channel, out var conversationId))
            {
                throw new ValidationException("Invalid channel");
            }

            try
            {
                // Direct conversations: only the two friends. Channels: only members of the server.
                await chatMessageService.ValidateUserCanAccessConversationAsync(conversationId, user.UserId, ct);
            }
            catch (ValidationException)
            {
                logger.LogWarning("User {UserId} cannot subscribe to {Channel}", user.UserId, message.Channel);
                throw new ValidationException("Cannot subscribe to this conversation");
            }

            await wsPubSubService.HandleSubscribeAsync(connectionId, nameof(WSHandlerType.ChatMessage), message.Channel, ct); //TO-DO Hardcoded subscribe to ChatMessage
        }
    }
}
