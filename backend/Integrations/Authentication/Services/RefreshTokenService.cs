using Dapper;
using HootOut.Authentication.Jwt;
using HootOut.CommonDomain.Persistence;
using Microsoft.Extensions.Options;
using System.Buffers.Text;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;

namespace HootOut.Authentication.Services
{
    public class RefreshTokenService : IRefreshTokenService
    {
        private JwtSettings jwtSettings;

        private sealed class ConsumedToken
        {
            public Guid UserId { get; set; }
            public Guid FamilyId { get; set; }
        }

        private IPersistenceProvider persistenceProvider;

        public RefreshTokenService(IOptions<JwtSettings> options,
            IPersistenceProvider persistanceProvider)
        {
            this.jwtSettings = options?.Value ?? throw new ArgumentNullException(nameof(options));
            this.persistenceProvider = persistanceProvider ?? throw new ArgumentNullException(nameof(persistenceProvider));
        }

        public async Task<string> IssueAsync(Guid userId, CancellationToken ct)
        {
            await using var conn = await persistenceProvider.OpenConnectionAsync(ct);
            return await InsertAsync(conn, null, userId, Guid.NewGuid(), ct);
        }

        public async Task<(Guid UserId, string NewToken)?> RotateAsync(string token, CancellationToken ct = default)
        {
            var hash = Hash(token);

            await using var conn = await persistenceProvider.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            // Atomic "use once": the WHERE clause is re-checked under the row lock, so if two requests
            // race with the same token, exactly one of them gets a row back.
            var consumed = await conn.QuerySingleOrDefaultAsync<ConsumedToken>(new CommandDefinition(
                """
            update "HootOut"."RefreshTokens"
            set "UsedAt" = now()
            where "TokenHash" = @hash
              and "UsedAt" is null
              and "RevokedAt" is null
              and "ExpiresAt" > now()
            returning "UserId" as UserId, "FamilyId" as FamilyId
            """,
                new { hash }, tx, cancellationToken: ct)); // TO-DO Check the returning "UserId" as UserId

            if (consumed is null)
            {
                // Not usable. If it was already used, somebody is replaying it: kill the whole session.
                // (A client that retries after a lost response also lands here. If that is a problem for
                // your mobile clients, allow a short grace period, e.g. used_at > now() - interval '10 seconds'.)
                var replayedFamily = await conn.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
                    """select "FamilyId" from "HootOut"."RefreshTokens" where "TokenHash" = @Hash and "UsedAt" is not null""",
                    new { Hash = hash }, tx, cancellationToken: ct));

                if (replayedFamily is { } familyId)
                {
                    await conn.ExecuteAsync(new CommandDefinition(
                        """update "HootOut"."RefreshTokens" set "RevokedAt" = now() where "FamilyId" = @FamilyId and "RevokedAt" is null""",
                        new { FamilyId = familyId }, tx, cancellationToken: ct));
                }

                await tx.CommitAsync(ct);
                return null;
            }

            var newToken = await InsertAsync(conn, tx, consumed.UserId, consumed.FamilyId, ct);
            await tx.CommitAsync(ct);

            return (consumed.UserId, newToken);
        }

        /// <summary>Logout: revokes the whole session the token belongs to.</summary>
        public async Task RevokeAsync(string token, CancellationToken ct = default)
        {
            await using var conn = await persistenceProvider.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                """
            update "HootOut"."RefreshTokens"
            set "RevokedAt" = now()
            where "RevokedAt" is null
              and "FamilyId" = (select "FamilyId" from "HootOut"."RefreshTokens" where "TokenHash" = @Hash)
            """,
                new { Hash = Hash(token) }, cancellationToken: ct));
        }

        /// <summary>Deletes tokens that expired more than a day ago. Called by the cleanup job.</summary>
        public async Task<int> DeleteExpiredAsync(CancellationToken ct = default)
        {
            await using var conn = await persistenceProvider.OpenConnectionAsync(ct);
            return await conn.ExecuteAsync(new CommandDefinition(
                """delete from "HootOut"."RefreshTokens" where "ExpiresAt" < now() - interval '1 day'""",
                cancellationToken: ct));
        }

        private async Task<string> InsertAsync(
            DbConnection conn, IDbTransaction? tx, Guid userId, Guid familyId, CancellationToken ct)
        {
            var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

            await conn.ExecuteAsync(new CommandDefinition(
                """
            insert into "HootOut"."RefreshTokens" ("TokenHash", "UserId", "FamilyId", "ExpiresAt")
            values (@Hash, @UserId, @familyId, now() + make_interval(days => @days))
            """,
                new
                {
                    Hash = Hash(token),
                    UserId = userId,
                    familyId,
                    days = jwtSettings.RefreshTokenDays
                },
                tx, cancellationToken: ct));

            return token;
        }

        private static string Hash(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
