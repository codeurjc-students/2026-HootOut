using HootOut.Contracts.WebSocket;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.Contracts.WebSockets.Services;
using Microsoft.Extensions.Logging;

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

            await pubSubService.SendMessageToChannel(message.Channel!, message);
        }
    }
}
