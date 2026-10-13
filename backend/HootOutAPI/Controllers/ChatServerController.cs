using HootOut.ChatChannels.Search;
using HootOut.ChatChannels.Services;
using HootOut.Contracts.ChatChannels.Services;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.ChatServers.Requests;
using HootOut.Contracts.ChatServers.Services;
using HootOut.HootOutAPI.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HootOut.HootOutAPI.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ChatServerController : ControllerBase
    {
        private readonly ILogger<ChatServerController> logger;
        private readonly IChatServerService chatServerService;

        public ChatServerController(
            ILogger<ChatServerController> logger,
            IChatServerService chatServerService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.chatServerService = chatServerService ?? throw new ArgumentNullException(nameof(chatServerService));
        }

        [HttpGet("all")]
        public async Task<ActionResult<IEnumerable<ChatServerDto>>> GetChatServersAsync(CancellationToken ct)
        {
            return await CommandLaunchHelper.LaunchAsync(
               logger,
               async () => await chatServerService.GetAllChatServerDtosAsync(ct),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        }

        [HttpPost]
        public async Task<ActionResult<ChatServerDto>> CreateChatServerAsync(CreateChatServerRequest request, CancellationToken ct)
        {
            var userId = User.FindFirstValue("sub");
            if (userId is null)
                return Unauthorized();

            return await CommandLaunchHelper.LaunchAsync(
               logger,
               async () => await chatServerService.CreateChatServerAsync(request, Guid.Parse(userId), ct),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        }
    }
}
