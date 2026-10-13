using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.HootOutAPI.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace HootOut.HootOutAPI.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ChatMessagesController : ControllerBase
    {
        private ILogger<ChatMessagesController> logger;
        private IChatMessageService chatMessageService;

        public ChatMessagesController(
            ILogger<ChatMessagesController> logger,
            IChatMessageService chatMessageService
            )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.chatMessageService = chatMessageService ?? throw new ArgumentNullException(nameof(chatMessageService));
        }

        [HttpGet("getByChannelId")]
        public async Task<ActionResult<IEnumerable<ChatMessageDto>>> GetChatMessagesByChannelIdAsync(Guid channelId, CancellationToken ct)
        {
            return await CommandLaunchHelper.LaunchAsync(
                logger,
                async () => await chatMessageService.GetMessagesByChannelAsync(channelId, ct),
                BadRequest,
                (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
                );
        }

    }
}
