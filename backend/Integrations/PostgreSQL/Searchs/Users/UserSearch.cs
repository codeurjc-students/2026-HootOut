using Dapper;
using HootOut.CommonDomain.Persistence;
using HootOut.Users.Entities;
using HootOut.Users.Search;

namespace HootOut.PostgreSQL.Searchs.Users
{
    public class UserSearch : IUserSearch
    {
        private IPersistenceProvider persistenceProvider;

        public UserSearch(IPersistenceProvider persistenceProvider)
        {
            this.persistenceProvider = persistenceProvider ?? throw new ArgumentNullException(nameof(persistenceProvider));
        }

        private const string UserColumns =
        """ "Id" as Id, "Username" as UserName, "PasswordHash" as Password, "Email" as Email""";
        public IEnumerable<UserInfo> GetAllUsers()
        {
            using (var conn = persistenceProvider.GetNewConnection())
            {
                return conn.Query<UserInfo>($"""SELECT {UserColumns} FROM "HootOut"."UserInfo" """);
            }
        }

        public UserInfo? GetUserByEmail(string email)
        {
            using (var conn = persistenceProvider.GetNewConnection())
            {
                return conn.QuerySingleOrDefault<UserInfo>(new CommandDefinition(
                    $"""SELECT {UserColumns} FROM "HootOut"."UserInfo" where lower("Email") = lower(@Email)""",
                     new { Email = email }));
            }
        }

        public UserInfo? GetUserById(Guid id)
        {
            using (var conn = persistenceProvider.GetNewConnection())
            {
                return conn.QuerySingleOrDefault<UserInfo>(new CommandDefinition(
                    $"""SELECT {UserColumns} FROM "HootOut"."UserInfo" where "Id" = @Id""",
                     new { Id = id }));
            }
        }
    }
}