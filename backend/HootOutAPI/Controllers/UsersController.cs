using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Services;
using HootOut.HootOutAPI.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HootOut.HootOutAPI.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly ILogger logger;
        private readonly IUserService userService;

        public UsersController(
            ILogger<UsersController> logger,
            IUserService userService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.userService = userService ?? throw new ArgumentNullException(nameof(userService));
        }

        [HttpGet("me")]
        public async Task<ActionResult<UserDto>> GetAuthorizedUserInfoAsync(CancellationToken ct)
        {
            ClaimsPrincipal principal = HttpContext.User;
            var userIdString = principal.FindFirstValue("sub");

            if (string.IsNullOrEmpty(userIdString))
            {
                return StatusCode(StatusCodes.Status401Unauthorized);
            }

            return await CommandLaunchHelper.LaunchAsync(
               logger,
               async () => await userService.GetUserDtoByIdAsync(Guid.Parse(userIdString), ct),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        }
    }
}
