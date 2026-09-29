using HootOut.Contracts.WebSocket;
using HootOut.WebSockets.Service;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace HootOut.HootOutWebsockets.Services
{
    public class ConnectionManager : IWSConnectionManager
    {
        private readonly ConcurrentDictionary<string, WebSocket> connections = new();

        private readonly ILogger<ConnectionManager> logger;
        private readonly WSMessageDeserializer webSocketMessageHandler;

        public ConnectionManager(ILogger<ConnectionManager> logger,
            WSMessageDeserializer websocketMessageHandler)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.webSocketMessageHandler = websocketMessageHandler ?? throw new ArgumentNullException(nameof(websocketMessageHandler));
        }

        public async Task AddConnectionAsync(WebSocket websocket, HttpContext httpContext)
        {
            var connectionId = Guid.CreateVersion7().ToString();

            connections.TryAdd(connectionId, websocket);

            string userId = "Usuario Test";

            logger.LogInformation("WebSocket connected: {ConnectionId} for user: {UserId}", connectionId, userId);

            try
            {
                await HandleMessagesAsync(websocket, connectionId, userId);
            }
            finally
            {
                connections.TryRemove(connectionId, out _);

                logger.LogInformation("Websocket disconnected: {ConnectionId} for user: {UserId}", connectionId, userId);
            }
        }

        private async Task HandleMessagesAsync(WebSocket websocket, string connectionId, string userId)
        {
            var buffer = new byte[1024 * 4];
            var messageBuffer = new StringBuilder();

            while (websocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;

                try
                {
                    result = await websocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None);

                }
                catch (WebSocketException ex)
                {
                    logger.LogWarning(ex, "WebSocket error for connection {ConnectionId} and user {UserId}", connectionId, userId);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await websocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None
                        );
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    messageBuffer.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }

                if (result.EndOfMessage)
                {
                    var message = messageBuffer.ToString();
                    messageBuffer.ToString();
                }
            }
        }

        public async Task SendMessageAsync(string connectionId, WebSocketMessage message)
        {
            if (!connections.TryGetValue(connectionId, out var webSocket))
            {
                return;
            }
            try
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    var rawMessage = JsonConvert.SerializeObject(message);
                    var bytes = Encoding.UTF8.GetBytes(rawMessage);
                    await webSocket.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        CancellationToken.None
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error sending message to connection {ConnectionId}", connectionId);
                throw;
            }
        }
    }
}
