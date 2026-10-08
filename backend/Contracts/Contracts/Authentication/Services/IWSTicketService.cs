using HootOut.Contracts.WebSockets.Dtos;

namespace HootOut.Contracts.Authentication.Services
{
    public interface IWSTicketService
    {
        Task<string> IssueAsync(Guid userId, string userName, TimeSpan lifetime, CancellationToken ct = default);

        Task<WSUserInfo?> ConsumeAsync(string ticket, CancellationToken ct = default);

        Task<int> DeleteExpiredAsync(CancellationToken ct = default);
        public static readonly TimeSpan DefaultLifetime = TimeSpan.FromSeconds(30);
    }
}
