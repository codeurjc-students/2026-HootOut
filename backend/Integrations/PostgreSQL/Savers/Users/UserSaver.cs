using Dapper;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common.Saver;
using HootOut.Users.Entities;

namespace HootOut.PostgreSQL.Savers.Users
{
    public class UserSaver : ISaver<UserInfo>
    {
        private IPersistenceProvider provider;

        public UserSaver(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private const string addOrReplaceUserInfo = """
            INSERT INTO "HootOut"."UserInfo" ("Id", "Username", "Email", "PasswordHash", "CreatedAt", "ModifiedAt") 
            VALUES (@Id, @Username, @Email, @PasswordHash, @CreatedAt, @ModifiedAt)
        """;

        public async Task SaveAsync(UserInfo item, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            try
            {
                var now = DateTime.UtcNow;
                await conn.ExecuteAsync(new CommandDefinition(
                    addOrReplaceUserInfo, new
                    {
                        item.Id,
                        item.Username,
                        item.Email,
                        PasswordHash = item.Password,
                        item.CreatedAt,
                        item.ModifiedAt
                    }, tx, cancellationToken: ct));
                await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                var msg = $"Error while trying to save user with uid {item.Id}";
                throw new Exception(msg, ex);
            }
        }
    }
}
