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

        public void Save(UserInfo item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            try
            {
                using (var conn = provider.GetNewConnection())
                {
                    var transaction = conn.BeginTransaction();

                    var now = DateTime.UtcNow;

                    conn.Execute(addOrReplaceUserInfo, new
                    {
                        item.Id,
                        item.Username,
                        item.Email,
                        PasswordHash = item.Password,
                        item.CreatedAt,
                        item.ModifiedAt
                    }, transaction);
                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                var msg = $"Error while trying to save user with uid {item.Id}";
                throw new Exception(msg, ex);
            }
        }
    }
}
