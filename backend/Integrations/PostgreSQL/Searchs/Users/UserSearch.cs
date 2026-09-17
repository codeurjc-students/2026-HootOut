using Dapper;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Search;

namespace HootOut.PostgreSQL.Searchs.Users
{
    public class UserSearch : IUserSearch
    {
        private IPersistenceProvider persistenceProvider;

        public UserSearch(IPersistenceProvider persistenceProvider)
        {
            this.persistenceProvider = persistenceProvider ?? throw new ArgumentException(nameof(persistenceProvider));
        }

        private const string getUsersQuery = """
            SELECT 
                "Uid",
                "Email",
                "Username"
            FROM "HootOut"."UserInfo"
        """;

        public IEnumerable<UserDto> GetUserDtos()
        {
            using (var conn = persistenceProvider.GetNewConnection())
            {
                return conn.Query<UserDto>(getUsersQuery);
            }
        }
    }
}