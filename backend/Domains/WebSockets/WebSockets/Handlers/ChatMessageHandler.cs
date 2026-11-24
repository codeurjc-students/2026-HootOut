using FluentValidation;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Entities;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.Contracts.WebSockets.Services;
using Newtonsoft.Json;

namespace HootOut.WebSockets.Actions
{
    public class ChatMessageHandler : IWSMessageHandler
    {

        public WSHandlerType Type => WSHandlerType.ChatMessage;
        private IWSPubSubService wsPubSubService;

        private IChatMessageService chatMessageService;

        public ChatMessageHandler(
            IWSPubSubService pubSubService,
            IChatMessageService chatMessageService)
        {
            this.wsPubSubService = pubSubService ?? throw new ArgumentNullException(nameof(pubSubService));
            this.chatMessageService = chatMessageService ?? throw new ArgumentNullException(nameof(chatMessageService));
        }

        public async Task HandleMessageAsync(Guid connectionId, WSUserInfo user, WebSocketMessage message, CancellationToken ct = default)
        {
            if (!Guid.TryParse(message.Channel, out var conversationId))
            {
                throw new ValidationException("Invalid channel");
            }

            CreateChatMessageRequest? request;
            try
            {
                request = string.IsNullOrWhiteSpace(message.Payload)
                    ? null
                    : JsonConvert.DeserializeObject<CreateChatMessageRequest>(message.Payload);
            }
            catch (JsonException)
            {
                request = null;
            }

            if (request == null)
            {
                throw new ValidationException("Invalid message");
            }

            // The conversation and the author always come from the socket, never from the payload
            request.ChatConversationId = conversationId;
            request.AuthorId = user.UserId;

            ChatMessageDto savedMessage = await chatMessageService.CreateChatMessageAsync(request, ct);

            savedMessage.Author?.Username = user.Username;

            message.Payload = JsonConvert.SerializeObject(savedMessage);

            await wsPubSubService.SendMessageAsync(nameof(WSHandlerType.ChatMessage), message.Channel!, message, ct); //Here the exchange is hardcoded because it is the correct one
        }
    }
}
