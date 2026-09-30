using HootOut.Contracts.WebSocket;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace HootOut.HootOutWebsockets.Services
{
    public class WSConnectionManager : IWSConnectionManager
    {
        private readonly ConcurrentDictionary<string, WebSocket> connections = new();

        private readonly ILogger<WSConnectionManager> logger;

        public WSConnectionManager(ILogger<WSConnectionManager> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> AddConnectionAsync(WebSocket websocket, HttpContext httpContext)
        {
            var connectionId = Guid.CreateVersion7().ToString();

            connections.TryAdd(connectionId, websocket);

            string userId = "Usuario Test";

            logger.LogInformation("WebSocket connected: {ConnectionId} for user: {UserId}", connectionId, userId);

            return connectionId;
        }

        public async Task<WebSocket?> GetWebSocketByConnectionIdAsync(string connectionId)
        {
            WebSocket? webSocket;
            if (connections.TryGetValue(connectionId, out webSocket))
            {
                return webSocket;
            }
            return webSocket;
        }

        public async Task SendMessageAsync(string connectionId, WebSocketMessage message)
        {
            var webSocket = await GetWebSocketByConnectionIdAsync(connectionId);

            if (webSocket == null)
            {
                return;
            }
            await SendMessageAsync(webSocket, connectionId, message);
        }

        public async Task SendMessageAsync(WebSocket webSocket, string connectionId, WebSocketMessage message)
        {
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

        public async Task RemoveConnectionAsync(string connectionId)
        {
            connections.TryRemove(connectionId, out _);
        }
    }
}
