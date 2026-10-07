using Dapper;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Authentication.Dtos;
using HootOut.Contracts.Authentication.Services;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace HootOut.Authentication.Services
{
    public class WSTicketService : IWSTicketService
    {
        private IPersistenceProvider provider;

        public WSTicketService(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public async Task<string> IssueAsync(Guid userId, string userName, TimeSpan lifetime, CancellationToken ct = default)
        {
            var ticket = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
            using var conn = await provider.OpenConnectionAsync(ct);

            await conn.ExecuteAsync(new CommandDefinition(
                """
            insert into "HootOut"."WSTickets" ("TicketHash", "UserId", "Username", "ExpiresAt")
            values (@hash, @userId, @userName, now() + make_interval(secs => @seconds))
            """,
                new
                {
                    hash = Hash(ticket),
                    userId,
                    userName,
                    seconds = lifetime.TotalSeconds
                },
                cancellationToken: ct));

            return ticket;
        }

        public async Task<WsTicketIdentityDto?> ConsumeAsync(string ticket, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.QuerySingleOrDefaultAsync<WsTicketIdentityDto>(new CommandDefinition(
                """
            delete from "HootOut"."WSTickets"
            where "TicketHash" = @hash and "ExpiresAt" > now()
            returning "UserId" as UserId, "Username" as Username
            """,
                new { hash = Hash(ticket) }, cancellationToken: ct));
        }

        public async Task<int> DeleteExpiredAsync(CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.ExecuteAsync(new CommandDefinition(
                """delete from "HootOut"."WSTickets" where "ExpiresAt" < now()""", cancellationToken: ct));
        }

        private static string Hash(string ticket) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ticket)));
    }
}
