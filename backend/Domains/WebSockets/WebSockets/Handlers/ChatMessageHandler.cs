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
            // Check userId can send to this channel.
            // Store message to DB
            // Processing...
            CreateChatMessageRequest request = JsonConvert.DeserializeObject<CreateChatMessageRequest>(message.Payload!)!;

            request.ChatChannelId = Guid.Parse(message.Channel!);
            request.AuthorId = user.UserId;

            ChatMessageDto savedMessage = chatMessageService.CreateChatMessage(request);
            savedMessage.Author?.Username = user.Username;

            message.Payload = JsonConvert.SerializeObject(savedMessage);

            await wsPubSubService.SendMessageAsync(nameof(WSHandlerType.ChatMessage), message.Channel!, message, ct); //Here the exchange is hardcoded because it is the right one
        }
    }
}
