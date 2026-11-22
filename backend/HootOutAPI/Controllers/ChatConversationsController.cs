using HootOut.Contracts.ChatChannels.Dtos;
using HootOut.Contracts.ChatConversations.Dtos;
using HootOut.Contracts.ChatConversations.Services;
using HootOut.HootOutAPI.Helpers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NLog.LayoutRenderers.Wrappers;
using System.Security.Claims;

namespace HootOut.HootOutAPI.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ChatConversationsController : ControllerBase
    {
        private ILogger<ChatConversationsController> logger;
        private IDirectConversationsService directConversationsService;

        public ChatConversationsController(ILogger<ChatConversationsController> logger,
            IDirectConversationsService directConversationsService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.directConversationsService = directConversationsService ?? throw new ArgumentNullException(nameof(directConversationsService));
        }

        [HttpGet("direct/{id:guid}")]
        public async Task<ActionResult<DirectConversationDto?>> GetDirectConversationByIdAsync([FromRoute] Guid id, CancellationToken ct)
        {
            var userId = User.FindFirstValue("sub");
            if (userId is null)
                return Unauthorized(); 

            return await CommandLaunchHelper.LaunchAsync(
               logger,
               async () => await directConversationsService.GetDirectConversationByIdAsync(id, Guid.Parse(userId), ct),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        } 
    }
}
