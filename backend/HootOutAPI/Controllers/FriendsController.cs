using HootOut.ChatServers.Services;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.ChatServers.Requests;
using HootOut.Contracts.Common;
using HootOut.Contracts.Friends.Dtos;
using HootOut.Contracts.Friends.Requests;
using HootOut.Contracts.Friends.Services;
using HootOut.HootOutAPI.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HootOut.HootOutAPI.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class FriendsController : ControllerBase
    {
        private readonly ILogger<FriendsController> logger;
        private readonly IFriendsService friendsService;

        public FriendsController(
            ILogger<FriendsController> logger,
            IFriendsService friendsService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.friendsService = friendsService ?? throw new ArgumentNullException(nameof(friendsService));
        }


        // Get Sent Friend Requests
        [HttpGet("requests/sent")]
        public async Task<ActionResult<PageResult<FriendRequestDto>>> GetSentFriendRequestsAsync(PageRequest request, CancellationToken ct)
        {
            var userId = User.FindFirstValue("sub");
            if (userId is null)
                return Unauthorized();
            return await CommandLaunchHelper.LaunchAsync(
               logger,
               async () => await friendsService.GetSentFriendRequestsAsync(request, Guid.Parse(userId), ct),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        }
        // Get Received Friend Requests
        [HttpGet("requests/received")]
        public async Task<ActionResult<PageResult<FriendRequestDto>>> GetReceivedFriendRequestsAsync(PageRequest request, CancellationToken ct)
        {
            var userId = User.FindFirstValue("sub");
            if (userId is null)
                return Unauthorized();
            return await CommandLaunchHelper.LaunchAsync(
               logger,
               async () => await friendsService.GetReceivedFriendRequestsAsync(request, Guid.Parse(userId), ct),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        }

        // Send Friend Request
        [HttpPost("requests")]
        public async Task<ActionResult<FriendRequestDto>> SendFriendRequestAsync(SendFriendRequest request, CancellationToken ct)
        {
            var userId = User.FindFirstValue("sub");
            if (userId is null)
                return Unauthorized();

            return await CommandLaunchHelper.LaunchAsync(
               logger,
               async () => await friendsService.SendFriendRequestAsync(request, Guid.Parse(userId), ct),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        }

        // UpdateFriendRequest
        [HttpPut("requests")]
        public async Task<ActionResult<FriendRequestDto>> UpdateFriendRequestAsync(UpdateFriendRequest request, CancellationToken ct)
        {
            var userId = User.FindFirstValue("sub");
            if (userId is null)
                return Unauthorized();

            return await CommandLaunchHelper.LaunchAsync(
               logger,
               async () => await friendsService.UpdateFriendRequestAsync(request, Guid.Parse(userId), ct),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        }


        // Get User Friends
        [HttpGet]
        public async Task<ActionResult<PageResult<FriendRelationDto>>> GetUserFriendsAsync(PageRequest request, CancellationToken ct)
        {
            var userId = User.FindFirstValue("sub");
            if (userId is null)
                return Unauthorized();
            return await CommandLaunchHelper.LaunchAsync(
               logger,
               async () => await friendsService.GetUserFriendsAsync(request, Guid.Parse(userId), ct),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        }
    }
}
