using HootOut.Contracts.Authentication.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.Contracts.Authentication.Services
{
    public interface IWSTicketService
    {
        Task<string> IssueAsync(Guid userId, string userName, TimeSpan lifetime, CancellationToken ct = default);

        Task<WsTicketIdentityDto?> ConsumeAsync(string ticket, CancellationToken ct = default);

        Task<int> DeleteExpiredAsync(CancellationToken ct = default);
        public static readonly TimeSpan DefaultLifetime = TimeSpan.FromSeconds(30);
    }
}
