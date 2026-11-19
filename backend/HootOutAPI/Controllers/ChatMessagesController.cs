using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Contracts.Common;
using HootOut.HootOutAPI.Helpers;
using Microsoft.AspNetCore.Mvc;

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

        [HttpGet]
        public async Task<ActionResult<PageResult<ChatMessageDto>>> GetMessagesByConverstionIdAsync([FromQuery] Guid conversationId, [FromQuery] Guid? next, [FromQuery] int? pageSize, CancellationToken ct)
        {
            return await CommandLaunchHelper.LaunchAsync(
                logger,
                async () => await chatMessageService.GetMessagesByConversationIdAsync(conversationId, next, pageSize ?? 10, ct),
                BadRequest,
                (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
                );
        }
    }
}
