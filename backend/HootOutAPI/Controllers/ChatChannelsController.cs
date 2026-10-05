using HootOut.Contracts.ChatChannels.Dtos;
using HootOut.Contracts.ChatChannels.Requests;
using HootOut.Contracts.ChatChannels.Services;
using HootOut.HootOutAPI.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace HootOut.HootOutAPI.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ChatChannelsController : ControllerBase
    {
        private ILogger<ChatChannelsController> logger;

        private IChatChannelsService chatChannelsService;

        public ChatChannelsController(
            ILogger<ChatChannelsController> logger,
            IChatChannelsService chatChannelsService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.chatChannelsService = chatChannelsService ?? throw new ArgumentNullException(nameof(chatChannelsService));
        }

        [HttpGet("all")]
        public ActionResult<IEnumerable<ChatChannelDto>> GetAllChatChannels()
        {
            return CommandLaunchHelper.Launch(
               logger,
               () => chatChannelsService.GetAllChatChannelsDtos(),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        }

        [HttpPost()]
        public ActionResult<IEnumerable<ChatChannelDto>> CreateChatChannel(CreateChatChannelRequest request)
        {
            return CommandLaunchHelper.Launch(
               logger,
               () => chatChannelsService.CreateChatChannel(request),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        }
    }
}
