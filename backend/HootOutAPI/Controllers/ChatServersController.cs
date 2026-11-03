using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.ChatServers.Requests;
using HootOut.Contracts.ChatServers.Services;
using HootOut.Contracts.Common;
using HootOut.HootOutAPI.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HootOut.HootOutAPI.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ChatServersController : ControllerBase
    {
        private readonly ILogger<ChatServersController> logger;
        private readonly IChatServerService chatServerService;

        public ChatServersController(
            ILogger<ChatServersController> logger,
            IChatServerService chatServerService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.chatServerService = chatServerService ?? throw new ArgumentNullException(nameof(chatServerService));
        }

        [HttpGet("joined")]
        public async Task<ActionResult<IEnumerable<ChatServerDto>>> GetChatServersAsync(CancellationToken ct)
        {
            var userId = User.FindFirstValue("sub");
            if (userId is null)
                return Unauthorized();
            return await CommandLaunchHelper.LaunchAsync(
               logger,
               async () => await chatServerService.GetServersByUserAsync(Guid.Parse(userId), ct),
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

        [HttpGet("{id:guid}/invitations")]
        public async Task<ActionResult<ChatServerDto>> GetServerInvitations([FromRoute] Guid id, [FromQuery] string? sortBy, [FromQuery] Guid? next, [FromQuery] bool descending, [FromQuery] int pageSize, CancellationToken ct)
        {
            var userId = User.FindFirstValue("sub");
            if (userId is null)
                return Unauthorized();

            var page = new PageRequest
            {
                Next = next,
                Descending = descending,
                PageSize = pageSize,
                SortBy = sortBy
            };

            return await CommandLaunchHelper.LaunchAsync(
               logger,
               async () => await chatServerService.GetServerInvitationsAsync(id, Guid.Parse(userId), page, ct),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        }

        [HttpPost("invitation")]
        public async Task<ActionResult> InviteServerMember(InviteServerMemberRequest request, CancellationToken ct)
        {
            var userId = User.FindFirstValue("sub");
            if (userId is null)
                return Unauthorized();

            return await CommandLaunchHelper.LaunchAsync(
               logger,
               async () => await chatServerService.InviteMemberAsync(request, Guid.Parse(userId), ct),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        }

        [HttpPut("invitation")]
        public async Task<ActionResult> UpdateServerMember(UpdateServerInvitationRequest request, CancellationToken ct)
        {
            var userId = User.FindFirstValue("sub");
            if (userId is null)
                return Unauthorized();

            return await CommandLaunchHelper.LaunchAsync(
               logger,
               async () => await chatServerService.UpdateServerInvitationAsync(request, Guid.Parse(userId), ct),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        }

        [HttpGet("{id:guid}/members")]
        public async Task<ActionResult<PageResult<ServerMemberDto>>> GetServerMembers([FromRoute] Guid id, [FromQuery] string? sortBy, [FromQuery] Guid? next, [FromQuery] bool descending, [FromQuery] int? pageSize , CancellationToken ct)
        {
            var userId = User.FindFirstValue("sub");
            if (userId is null)
                return Unauthorized();

            var page = new PageRequest
            {
                Next = next,
                Descending = descending,
                PageSize = pageSize ?? 10,
                SortBy = sortBy
            };

            return await CommandLaunchHelper.LaunchAsync(
               logger,
               async () => await chatServerService.GetServerMemberDtosAsync(id, Guid.Parse(userId), page, ct),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ChatServerDto>> GetChatServer([FromRoute] Guid id, CancellationToken ct)
        {
            var userId = User.FindFirstValue("sub");
            if (userId is null)
                return Unauthorized();

            return await CommandLaunchHelper.LaunchAsync(
               logger,
               async () => await chatServerService.GetServerDtoByIdAsync(id, Guid.Parse(userId), ct),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        }
    }
}
