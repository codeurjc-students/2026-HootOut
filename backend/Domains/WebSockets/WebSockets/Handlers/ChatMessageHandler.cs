using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.WebSocket;
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

        public ChatMessageHandler(
            ILogger<ChatMessageHandler> logger,
            IWSPubSubService pubSubService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.pubSubService = pubSubService ?? throw new ArgumentNullException(nameof(pubSubService));
        }

        public async Task HandleMessageAsync(string connectionId, string userId, WebSocketMessage message)
        {
            // Check userId can send to this channel.
            // Store message to DB
            // Processing...
            ChatMessageDto chatMessage = JsonConvert.DeserializeObject<ChatMessageDto>(message.Payload);

            chatMessage.Id = Guid.CreateVersion7();
            chatMessage.CreatedAt = DateTime.UtcNow;
            chatMessage.ModifiedAt = chatMessage.ModifiedAt;

            message.Payload = JsonConvert.SerializeObject(chatMessage);

            await pubSubService.SendMessageToChannel(message.Channel!, message);
        }
    }
}
