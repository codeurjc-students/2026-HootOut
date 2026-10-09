using HootOut.Contracts.Authentication.Services;
using HootOut.Contracts.WebSockets.Services;
using HootOut.HootOutWebsockets.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace HootOut.HootOutWebsockets.Controllers
{
    public class WebSocketController : ControllerBase
    {
        private readonly ILogger logger;
        private readonly IWSConnectionManager connectionManager;
        private readonly IWSConnectionHandler connectionHandler;
        private readonly IWSPubSubService pubSubService;

        private readonly IWSTicketService ticketService;

        public WebSocketController(
            ILogger<WebSocketController> logger,
            IWSConnectionManager connectionManager,
            IWSConnectionHandler connectionHandler,
            IWSPubSubService pubSubService,
            IWSTicketService ticketService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            this.connectionHandler = connectionHandler ?? throw new ArgumentNullException(nameof(connectionHandler));
            this.pubSubService = pubSubService ?? throw new ArgumentNullException(nameof(pubSubService));
            this.ticketService = ticketService ?? throw new ArgumentNullException(nameof(ticketService));
        }

        [Route("/ws")]
        public async Task Get([FromQuery] string? ticket = null)
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            { 
                var user = string.IsNullOrEmpty(ticket)
                    ? null
                    : await ticketService.ConsumeAsync(ticket, HttpContext.RequestAborted);

                if (user == null)
                {
                    HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                var connectionId = await connectionManager.AddConnectionAsync(webSocket, user);
                try
                {
                    await connectionHandler.ReadMessagesAsync(webSocket, connectionId, user);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Exception during Websockets communication");
                    throw;
                }
                finally
                {
                    await connectionManager.RemoveConnectionAsync(connectionId);
                    await pubSubService.RemoveAllConnectionSubscriptions(connectionId);
                }
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }
    }
}