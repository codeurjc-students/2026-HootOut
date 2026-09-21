using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;

namespace HootOut.HootOutWebsockets.Controllers
{
    public class WebSocketController : ControllerBase
    {
        private ILogger logger { get; set; }

        public WebSocketController(ILogger<WebSocketController> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Route("/ws")]
        public async Task Get()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                try
                {
                    await Echo(webSocket);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Exception during Websockets communication");
                    throw ex;
                }
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        private static async Task Echo(WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            var receiveResult = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);

            while (!receiveResult.CloseStatus.HasValue)
            {
                var str = System.Text.Encoding.Default.GetString(buffer, 0, receiveResult.Count);
                str = "Hello from the Server " + str;
                str = str.ToUpperInvariant();
                var msg = System.Text.Encoding.Default.GetBytes(str);

                await webSocket.SendAsync(
                    new ArraySegment<byte>(msg, 0, msg.Length),
                    receiveResult.MessageType,
                    receiveResult.EndOfMessage,
                    CancellationToken.None);

                receiveResult = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), CancellationToken.None);
            }

            await webSocket.CloseAsync(
                receiveResult.CloseStatus.Value,
                receiveResult.CloseStatusDescription,
                CancellationToken.None);
        }
    }
}