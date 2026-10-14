using Dapper;
using HootOut.CommonDomain.Persistence;
using HootOut.Users.Entities;
using HootOut.Users.Search;

namespace HootOut.PostgreSQL.Searchs.Users
{
    public class UserSearch : IUserSearch
    {
        private IPersistenceProvider provider;

        public UserSearch(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private const string UserColumns =
        """ "Id" as Id, "Username" as Username, "PasswordHash" as Password, "Email" as Email""";
        public async Task<IEnumerable<UserInfo>> GetAllUsersAsync(CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            return await conn.QueryAsync<UserInfo>($"""SELECT {UserColumns} FROM "HootOut"."UserInfo" """, ct);
        }

        public async Task<UserInfo?> GetUserByEmailAsync(string email, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            return await conn.QuerySingleOrDefaultAsync<UserInfo>(new CommandDefinition(
                $"""SELECT {UserColumns} FROM "HootOut"."UserInfo" where lower("Email") = lower(@Email)""",
                 new { Email = email }, cancellationToken: ct));
        }

        public async Task<UserInfo?> GetUserByIdAsync(Guid id, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            return await conn.QuerySingleOrDefaultAsync<UserInfo>(new CommandDefinition(
                $"""SELECT {UserColumns} FROM "HootOut"."UserInfo" where "Id" = @Id""",
                    new { Id = id }, cancellationToken: ct));
        }
    }
}