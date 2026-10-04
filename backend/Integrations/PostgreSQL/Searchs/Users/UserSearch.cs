using Dapper;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Search;
using HootOut.Users.Entities;

namespace HootOut.PostgreSQL.Searchs.Users
{
    public class UserSearch : IUserSearch
    {
        private IPersistenceProvider persistenceProvider;

        public UserSearch(IPersistenceProvider persistenceProvider)
        {
            this.persistenceProvider = persistenceProvider ?? throw new ArgumentNullException(nameof(persistenceProvider));
        }

        private const string getUsersQuery = """
            SELECT 
                "Id",
                "Email",
                "Username"
            FROM "HootOut"."UserInfo"
        """;

        public IEnumerable<UserInfo> GetAllUsers()
        {
            using (var conn = persistenceProvider.GetNewConnection())
            {
                return conn.Query<UserInfo>(getUsersQuery);
            }
        }
    }
}