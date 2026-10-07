using HootOut.Contracts.Authentication.Requests;
using HootOut.Contracts.Authentication.Responses;
using HootOut.Contracts.Authentication.Services;
using HootOut.HootOutAPI.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HootOut.HootOutAPI.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthenticationController : ControllerBase
    {
        private ILogger<AuthenticationController> logger;
        private IAuthenticationService authService;

        public AuthenticationController(ILogger<AuthenticationController> logger,
            IAuthenticationService authService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Register(AuthRegistrationRequest request, CancellationToken ct)
        {
            return await CommandLaunchHelper.LaunchAsync(
                logger,
                async () => await authService.RegisterUserAsync(request, ct),
                BadRequest,
                (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
            );
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Login(AuthRequest request, CancellationToken ct)
        {
            return await CommandLaunchHelper.LaunchAsync(
                logger,
                async () => await authService.AuthenticateUserAsync(request, ct),
                BadRequest,
                (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
            );
        }


        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> RefreshJWT(AuthRefreshRequest request, CancellationToken ct)
        {
            return await CommandLaunchHelper.LaunchAsync(
                logger,
                async () => await authService.RefreshTokenAsync(request, ct),
                BadRequest,
                (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
            );
        }

        [HttpPost("logout")]
        [AllowAnonymous]
        public async Task<ActionResult> Logout(AuthRefreshRequest request, CancellationToken ct)
        {
            return await CommandLaunchHelper.LaunchAsync(
                logger,
                async () => await authService.RevokeAsync(request, ct),
                BadRequest,
                (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
            );
        }

        [HttpPost("ws-ticket")]
        [Authorize]
        public async Task<ActionResult<WSTicketResponse>> CreateWebSocketTicket(CancellationToken ct)
        {
            var userId = User.FindFirstValue("sub");
            var userName = User.FindFirstValue("name");
            if (userId is null || userName is null)
                return Unauthorized(); 

            return await CommandLaunchHelper.LaunchAsync(
                logger,
                async () => await authService.CreateWebSocketTicketAsync(Guid.Parse(userId), userName, ct),
                BadRequest,
                (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
            );
        }
    }
}
