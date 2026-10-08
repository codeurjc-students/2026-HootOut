using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Contracts.WebSocket;
using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.Contracts.WebSockets.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace HootOut.WebSockets.Actions
{
    public class ChatMessageHandler : IWSMessageHandler
    {
        public WSHandlerType Type => WSHandlerType.ChatMessage;

        private ILogger<ChatMessageHandler> logger;
        private IWSPubSubService pubSubService;

        private IChatMessageService chatMessageService;

        public ChatMessageHandler(
            ILogger<ChatMessageHandler> logger,
            IWSPubSubService pubSubService,
            IChatMessageService chatMessageService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.pubSubService = pubSubService ?? throw new ArgumentNullException(nameof(pubSubService));
            this.chatMessageService = chatMessageService ?? throw new ArgumentNullException(nameof(chatMessageService));
        }

        public async Task HandleMessageAsync(Guid connectionId, WSUserInfo user, WebSocketMessage message)
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

            await pubSubService.SendMessageToChannel(connectionId, message.Channel!, message);
        }
    }
}
