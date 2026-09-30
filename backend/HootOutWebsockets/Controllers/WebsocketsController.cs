using HootOut.HootOutWebsockets.Services;
using Microsoft.AspNetCore.Mvc;

namespace HootOut.HootOutWebsockets.Controllers
{
    public class WebSocketController : ControllerBase
    {
        private ILogger logger { get; set; }
        private IWSConnectionManager connectionManager;

        private IWSConnectionHandler connectionHandler;

        public WebSocketController(
            ILogger<WebSocketController> logger,
            IWSConnectionManager connectionManager,
            IWSConnectionHandler connectionHandler)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            this.connectionHandler = connectionHandler ?? throw new ArgumentNullException(nameof(connectionHandler));
        }

        [Route("/ws")]
        public async Task Get()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                var connectionId = await connectionManager.AddConnectionAsync(webSocket, HttpContext);
                try
                {
                    await connectionHandler.ReadMessagesAsync(webSocket, connectionId, "");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Exception during Websockets communication");
                    throw;
                }
                finally
                {
                    await connectionManager.RemoveConnectionAsync(connectionId);
                }
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }
    }
}