using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Services;
using HootOut.HootOutAPI.Helpers;
using Microsoft.AspNetCore.Mvc;

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

        [HttpGet("all")]
        public ActionResult<IEnumerable<UserDto>> GetUserList()
        {
            return CommandLaunchHelper.Launch(
               logger,
               () => userService.GetUserDtos(),
               BadRequest,
               (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
           );
        }
    }
}
