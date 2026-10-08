using HootOut.Contracts.Authentication.Requests;
using HootOut.Contracts.Authentication.Responses;
using HootOut.Contracts.Authentication.Services;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.Contracts.Users.Services;
using HootOut.Users.Entities;
using HootOut.Users.Mappers;
using HootOut.Users.Search;
using System.ComponentModel.DataAnnotations;

namespace HootOut.Authentication.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IUserService userService;
        private readonly IUserSearch userSearch;
        private readonly IPasswordHasherService passwordHasherService;
        private readonly IAuthTokenService authTokenService;
        private readonly IRefreshTokenService refreshTokenService;
        private readonly IWSTicketService ticketService;

        public AuthenticationService(
            IUserService userService,
            IUserSearch userSearch,
            IPasswordHasherService passwordHasherService,
            IAuthTokenService authTokenService,
            IRefreshTokenService refreshTokenService,
            IWSTicketService ticketService
        )
        {
            this.userService = userService ?? throw new ArgumentNullException(nameof(userService));
            this.userSearch = userSearch ?? throw new ArgumentNullException(nameof(userSearch));
            this.passwordHasherService = passwordHasherService ?? throw new ArgumentNullException(nameof(passwordHasherService));
            this.authTokenService = authTokenService ?? throw new ArgumentNullException(nameof(authTokenService));
            this.refreshTokenService = refreshTokenService ?? throw new ArgumentNullException(nameof(refreshTokenService));
            this.ticketService = ticketService ?? throw new ArgumentNullException(nameof(ticketService));
        }



        public async Task<AuthResponse> AuthenticateUserAsync(AuthRequest request, CancellationToken ct = default)
        {
            //TO-DO proper user validation
            if (string.IsNullOrEmpty(request.Email))
            {
                throw new ValidationException("Invalid Email");
            }

            if (string.IsNullOrEmpty(request.Password))
                throw new ValidationException("Invalid Password");

            UserInfo? user = userSearch.GetUserByEmail(request.Email);

            var validPassword = passwordHasherService.VerifyPassword(request.Password, user?.Password ?? passwordHasherService.DummyHash);

            if (user == null || !validPassword)
            {
                throw new ValidationException("");
            }

            return new AuthResponse
            {
                AccessToken = authTokenService.CreateAccessToken(user.ToDto()),
                TokenType = "Bearer",
                ExpiresIn = authTokenService.ExpiresInSeconds,
                RefreshToken = await refreshTokenService.IssueAsync(user.Id, ct)
            };
        }

        public async Task<AuthResponse> RegisterUserAsync(AuthRegistrationRequest request, CancellationToken ct = default)
        {
            //TO-DO proper user validation
            var userName = request.Username?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(userName) || userName.Length < 3 || userName.Length > 50)
                throw new ValidationException("Invalid Username");

            if (string.IsNullOrEmpty(request.Email))
            {
                throw new ArgumentOutOfRangeException("Invalid Email");
            }

            if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 5)
                throw new ValidationException("Invalid Password");

            var passwordHash = passwordHasherService.HashPassword(request.Password);

            var newUser = userService.CreateUser(new CreateUserRequest
            {
                Username = userName,
                Email = request.Email,
                Password = passwordHash
            });

            return new AuthResponse
            {
                AccessToken = authTokenService.CreateAccessToken(newUser),
                TokenType = "Bearer",
                ExpiresIn = authTokenService.ExpiresInSeconds,
                RefreshToken = await refreshTokenService.IssueAsync(newUser.Id, ct)
            };
        }

        public async Task<AuthResponse> RefreshTokenAsync(AuthRefreshRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(request.RefreshToken))
                throw new ValidationException("Invalid Token"); // TO-DO proper validation

            var rotated = await refreshTokenService.RotateAsync(request.RefreshToken, ct);
            if (rotated is null)
                throw new ValidationException("Invalid Token");

            // Re-load the user so disabled/deleted accounts and role changes take effect.
            var user = userSearch.GetUserById(rotated.Value.UserId);
            if (user is null)
            {
                await refreshTokenService.RevokeAsync(rotated.Value.NewToken, ct);
                throw new ValidationException("Invalid Token");
            }

            return new AuthResponse
            {
                AccessToken = authTokenService.CreateAccessToken(user.ToDto()),
                TokenType = "Bearer",
                ExpiresIn = authTokenService.ExpiresInSeconds,
                RefreshToken = rotated.Value.NewToken
            };
        }

        public async Task RevokeAsync(AuthRefreshRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(request.RefreshToken))
            {
                throw new ArgumentNullException(nameof(request.RefreshToken));
            }

            await refreshTokenService.RevokeAsync(request.RefreshToken, ct);
        }

        public async Task<WSTicketResponse> CreateWebSocketTicketAsync(Guid userId, string username, CancellationToken ct)
        {
            var ticket = await ticketService.IssueAsync(userId, username, IWSTicketService.DefaultLifetime, ct);
            return new WSTicketResponse
            {
                Ticket = ticket,
                ExpiresIn = (int)IWSTicketService.DefaultLifetime.TotalSeconds
            };
        }
    }
}
