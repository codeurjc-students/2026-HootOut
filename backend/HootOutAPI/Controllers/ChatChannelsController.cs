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

        [HttpGet("getByServerId")]
        public async Task<ActionResult<IEnumerable<ChatChannelDto>>> GetChannelsById(Guid serverId, CancellationToken ct)
        {
            return await CommandLaunchHelper.LaunchAsync(
               logger,
               async () => await chatChannelsService.GetChannelsByServerIdAsync(serverId, ct),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        }

        [HttpPost()]
        public async Task<ActionResult<ChatChannelDto>> CreateChatChannel(CreateChatChannelRequest request, CancellationToken ct)
        {
            return await CommandLaunchHelper.LaunchAsync(
               logger,
               async () => await chatChannelsService.CreateChatChannelAsync(request, ct),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        }

        [HttpPut()]
        public async Task<ActionResult<ChatChannelDto>> UpdateChatChannel(UpdateChatChannelRequest request, CancellationToken ct)
        {
            return await CommandLaunchHelper.LaunchAsync(
               logger,
               async () => await chatChannelsService.UpdateChatChannelAsync(request, ct),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<Guid>> DeleteChatChannel(Guid id, CancellationToken ct)
        {
            return await CommandLaunchHelper.LaunchAsync(
               logger,
               async () => await chatChannelsService.DeleteChatChannelAsync(id, ct),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        }
    }
}
