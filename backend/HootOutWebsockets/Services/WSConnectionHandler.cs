using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Services;
using HootOut.HootOutWebsockets.Contracts;
using System.Net.WebSockets;
using System.Text;

namespace HootOut.HootOutWebsockets.Services
{
    public class WSConnectionHandler : IWSConnectionHandler
    {
        /// <summary>
        /// Biggest text message accepted, in bytes. A 6000 character message is escaped twice
        /// (the payload is a JSON string inside the JSON envelope), so it can reach ~42 KB plus the attachments.
        /// </summary>
        public const int MaxMessageSize = 64 * 1024;

        private const int ReceiveBufferSize = 4 * 1024;

        public static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);

        // Throws on invalid UTF-8 instead of replacing it with '?'
        private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        private ILogger<WSConnectionHandler> logger;
        private IWSMessageDeserializer wsMessageDeserializer;

        public WSConnectionHandler(ILogger<WSConnectionHandler> logger,
            IWSMessageDeserializer wsMessageDeserializer)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.wsMessageDeserializer = wsMessageDeserializer ?? throw new ArgumentNullException(nameof(wsMessageDeserializer));
        }

        public async Task ReadMessagesAsync(WebSocket websocket, Guid connectionId, WSUserInfo user)
        {
            var buffer = new byte[ReceiveBufferSize];

            // Bytes, not text: a UTF-8 character can be split between two chunks,
            // so the message is only decoded once it is complete
            using var message = new MemoryStream();

            while (websocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;

                try
                {
                    result = await websocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None);
                }
                catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException)
                {
                    // Connection lost or socket aborted (e.g. by WSConnectionManager for a slow client)
                    logger.LogWarning(ex, "WebSocket error for connection {ConnectionId} and user {UserId}", connectionId, user.UserId);
                    return;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await CloseAsync(websocket, WebSocketCloseStatus.NormalClosure, "Closing", connectionId);
                    return;
                }

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    logger.LogWarning("Binary message from connection {ConnectionId} and user {UserId}", connectionId, user.UserId);
                    await CloseAsync(websocket, WebSocketCloseStatus.InvalidMessageType, "Only text messages are supported", connectionId);
                    return;
                }

                if (message.Length + result.Count > MaxMessageSize)
                {
                    logger.LogWarning("Message too big from connection {ConnectionId} and user {UserId}", connectionId, user.UserId);
                    await CloseAsync(websocket, WebSocketCloseStatus.MessageTooBig, "Message too big", connectionId);
                    return;
                }

                message.Write(buffer, 0, result.Count);

                if (result.EndOfMessage)
                {
                    string text;
                    try
                    {
                        text = StrictUtf8.GetString(message.GetBuffer(), 0, (int)message.Length);
                    }
                    catch (DecoderFallbackException)
                    {
                        logger.LogWarning("Invalid UTF-8 from connection {ConnectionId} and user {UserId}", connectionId, user.UserId);
                        await CloseAsync(websocket, WebSocketCloseStatus.InvalidPayloadData, "Invalid UTF-8", connectionId);
                        return;
                    }
                    finally
                    {
                        message.SetLength(0);
                    }

                    await wsMessageDeserializer.HandleMessageAsync(connectionId, user, text);
                }
            }
        }

        private async Task CloseAsync(WebSocket websocket, WebSocketCloseStatus status, string description, Guid connectionId)
        {
            try
            {
                if (websocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    // A client that does not answer the close handshake must not keep the connection alive
                    using var timeout = new CancellationTokenSource(CloseTimeout);
                    await websocket.CloseAsync(status, description, timeout.Token);
                }
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException)
            {
                logger.LogWarning(ex, "Error closing connection {ConnectionId}", connectionId);
                websocket.Abort();
            }
        }
    }
}
