namespace HootOut.Authentication.Services
{
    public interface IRefreshTokenService
    {
        Task<string> IssueAsync(Guid userId, CancellationToken ct);
        Task<(Guid UserId, string NewToken)?> RotateAsync(string token, CancellationToken ct = default);
        Task RevokeAsync(string token, CancellationToken ct = default);
        Task<int> DeleteExpiredAsync(CancellationToken ct = default);
    }
}
