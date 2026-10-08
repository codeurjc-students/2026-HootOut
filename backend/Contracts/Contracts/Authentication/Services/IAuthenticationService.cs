using HootOut.Contracts.Authentication.Requests;
using HootOut.Contracts.Authentication.Responses;

namespace HootOut.Contracts.Authentication.Services
{
    public interface IAuthenticationService
    {
        Task<AuthResponse> AuthenticateUserAsync(AuthRequest request, CancellationToken ct);
        Task<AuthResponse> RefreshTokenAsync(AuthRefreshRequest request, CancellationToken ct);
        Task<AuthResponse> RegisterUserAsync(AuthRegistrationRequest request, CancellationToken ct);
        Task RevokeAsync(AuthRefreshRequest request, CancellationToken ct);

        Task<WSTicketResponse> CreateWebSocketTicketAsync(Guid userId, string username, CancellationToken ct);
    }
}
