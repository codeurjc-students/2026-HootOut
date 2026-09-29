using HootOut.HootOutWebsockets.Services;
using Microsoft.AspNetCore.Mvc;

namespace HootOut.HootOutWebsockets.Controllers
{
    public class WebSocketController : ControllerBase
    {
        private ILogger logger { get; set; }
        private IWSConnectionManager connectionManager;

        public WebSocketController(
            ILogger<WebSocketController> logger,
            IWSConnectionManager connectionManager)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        }

        [Route("/ws")]
        public async Task Get()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                try
                {
                    await connectionManager.AddConnectionAsync(webSocket, HttpContext);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Exception during Websockets communication");
                    throw;
                }
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }
    }
}