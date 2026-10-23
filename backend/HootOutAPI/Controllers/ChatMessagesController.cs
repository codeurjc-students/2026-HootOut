using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Contracts.Common;
using HootOut.HootOutAPI.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HootOut.HootOutAPI.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ChatMessagesController : ControllerBase
    {
        private readonly ILogger<ChatMessagesController> logger;
        private readonly IChatMessageService chatMessageService;

        public ChatMessagesController(
            ILogger<ChatMessagesController> logger,
            IChatMessageService chatMessageService
            )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.chatMessageService = chatMessageService ?? throw new ArgumentNullException(nameof(chatMessageService));
        }

        [HttpGet("channelMessages")]
        public async Task<ActionResult<PageResult<ChatMessageDto>>> GetChatMessagesByChannelIdAsync([FromQuery] Guid channelId, [FromQuery] Guid? next, [FromQuery] int? pageSize, CancellationToken ct)
        {
            return await CommandLaunchHelper.LaunchAsync(
                logger,
                async () => await chatMessageService.GetMessagesByChannelAsync(channelId, next, pageSize ?? 10, ct),
                BadRequest,
                (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
                );
        } 
    }
}
