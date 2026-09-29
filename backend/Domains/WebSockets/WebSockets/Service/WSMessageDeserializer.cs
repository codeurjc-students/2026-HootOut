using HootOut.Contracts.WebSocket;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.Contracts.WebSockets.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace HootOut.WebSockets.Service
{
    public class WSMessageDeserializer : IWSMessageDeserializer
    {
        private ILogger<WSMessageDeserializer> logger;
        private IEnumerable<IWSMessageHandler> wsMessageHandlers;

        public WSMessageDeserializer(ILogger<WSMessageDeserializer> logger,
            IEnumerable<IWSMessageHandler> wsMessageHandlers)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.wsMessageHandlers = wsMessageHandlers ?? throw new ArgumentNullException(nameof(wsMessageHandlers));
        }
        public async Task HandleMessageAsync(string connectionId, string userId, string rawMessage)
        {
            WebSocketMessage? message;
            try
            {
                message = JsonConvert.DeserializeObject<WebSocketMessage>(rawMessage);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Invalid JSON from connection {ConnectionId} for user {UserId}", connectionId, userId);
                return;
            }

            if (message == null)
            {
                logger.LogWarning("Empty Message from connection {ConnectionId} for user {UserId}", connectionId, userId);
                return;
            }

            if (message.Type == 0)
            {
                logger.LogWarning("Missing Message Type for connection {ConnectionId} and user {UserId}", connectionId, userId);
            }

            wsMessageHandlers.Where(x => x.Type == message.Type).ToList().ForEach(async x => await x.HandleMessageAsync(connectionId, userId, message)); // TO-DO Check concurrency
        }
    }
}
