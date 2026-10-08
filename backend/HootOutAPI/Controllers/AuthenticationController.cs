using HootOut.Authentication.Jwt;
using HootOut.Contracts.Authentication.Requests;
using HootOut.Contracts.Authentication.Responses;
using HootOut.Contracts.Authentication.Services;
using HootOut.HootOutAPI.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace HootOut.HootOutAPI.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthenticationController : ControllerBase
    {
        private const string RefreshCookieName = "refresh_token";
        private const string RefreshCookiePath = "/api/v1/auth";
        private const string DeliveryHeader = "X-Token-Delivery";
        private const string DeliveryCookie = "cookie";

        private ILogger<AuthenticationController> logger;
        private JwtSettings jwtSettings;
        private IAuthenticationService authService;

        public AuthenticationController(ILogger<AuthenticationController> logger,
            IAuthenticationService authService,
            IOptions<JwtSettings> jwtOptions)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
            this.jwtSettings = jwtOptions?.Value ?? throw new ArgumentNullException(nameof(jwtOptions));
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Register(AuthRegistrationRequest request, CancellationToken ct)
        {
            var cookieMode = WantsCookie;
            return await CommandLaunchHelper.LaunchAsync(
                logger,
                async () => DeliverTokens(await authService.RegisterUserAsync(request, ct), cookieMode),
                BadRequest,
                (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
            );
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Login(AuthRequest request, CancellationToken ct)
        {
            var cookieMode = WantsCookie;
            return await CommandLaunchHelper.LaunchAsync(
                logger,
                async () => DeliverTokens(await authService.AuthenticateUserAsync(request, ct), cookieMode),
                BadRequest,
                (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
            );
        }


        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> RefreshJWT(
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AuthRefreshRequest? request,
            CancellationToken ct)
        {
            var cookieMode = WantsCookie;
            var refreshToken = cookieMode ? Request.Cookies[RefreshCookieName] : request?.RefreshToken;

            if (string.IsNullOrEmpty(refreshToken))
                return BadRequest("A refresh token is required.");

            return await CommandLaunchHelper.LaunchAsync(
                logger,
                async () => DeliverTokens(
                    await authService.RefreshTokenAsync(new AuthRefreshRequest { RefreshToken = refreshToken }, ct)
                    , cookieMode),
                err =>
                {
                    if (cookieMode) ClearRefreshCookie();
                    return BadRequest(err);
                },
                (err) => StatusCode(StatusCodes.Status500InternalServerError, err)
            );
        }

        [HttpPost("logout")]
        [AllowAnonymous]
        public async Task<ActionResult> Logout([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AuthRefreshRequest? request, CancellationToken ct)
        {
            var cookieMode = WantsCookie;
            var refreshToken = cookieMode ? Request.Cookies[RefreshCookieName] : request?.RefreshToken;
            if (cookieMode) ClearRefreshCookie();

            if (string.IsNullOrEmpty(refreshToken))
                return NoContent();

            return await CommandLaunchHelper.LaunchAsync(
                logger,
                async () => await authService.RevokeAsync(new AuthRefreshRequest { RefreshToken = refreshToken }, ct),
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

        // ------------------------------------------------------------------
        // Refresh token delivery
        // ------------------------------------------------------------------

        private bool WantsCookie =>
            string.Equals(Request.Headers[DeliveryHeader].ToString(), DeliveryCookie, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Body mode: returns the response untouched.
        /// Cookie mode: moves the refresh token into the HttpOnly cookie and blanks it in the JSON.
        /// </summary>
        private AuthResponse DeliverTokens(AuthResponse auth, bool cookieMode)
        {
            if (!cookieMode)
                return auth;

            Response.Cookies.Append(
                RefreshCookieName,
                auth.RefreshToken,
                BuildCookieOptions(TimeSpan.FromDays(jwtSettings.RefreshTokenDays)));

            auth.RefreshToken = string.Empty;   // never expose it to the page's JavaScript
            return auth;
        }

        private void ClearRefreshCookie() =>
            Response.Cookies.Delete(RefreshCookieName, BuildCookieOptions(maxAge: null));

        // Delete only works if Path (and the other attributes) match the cookie that was set.
        private static CookieOptions BuildCookieOptions(TimeSpan? maxAge) => new()
        {
            HttpOnly = true,                    // not readable from JavaScript
            Secure = true,                      // HTTPS only
            SameSite = SameSiteMode.Strict,     // not sent on cross-site requests
            Path = RefreshCookiePath,           // only sent to /api/v1/auth/*, not to every API call
            MaxAge = maxAge
        };
    }
}
