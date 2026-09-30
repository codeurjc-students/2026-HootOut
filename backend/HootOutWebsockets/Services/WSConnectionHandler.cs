using HootOut.Contracts.WebSockets.Services;
using System.Net.WebSockets;
using System.Text;

namespace HootOut.HootOutWebsockets.Services
{
    public class WSConnectionHandler : IWSConnectionHandler
    {
        private ILogger<WSConnectionHandler> logger;
        private IWSMessageDeserializer wsMessageDeserializer;

        public WSConnectionHandler(ILogger<WSConnectionHandler> logger,
            IWSMessageDeserializer wSMessageDeserializer)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.wsMessageDeserializer = wSMessageDeserializer ?? throw new ArgumentNullException(nameof(wsMessageDeserializer));
        }

        public async Task ReadMessagesAsync(WebSocket websocket, string connectionId, string userId)
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
                    return;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    messageBuffer.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }

                if (result.EndOfMessage)
                {
                    var message = messageBuffer.ToString();
                    messageBuffer.Clear();
                    await wsMessageDeserializer.HandleMessageAsync(connectionId, userId, message);
                }
            }
        }
    }
}
