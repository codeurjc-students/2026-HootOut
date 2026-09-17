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
            INSERT INTO "HootOut"."UserInfo" ("Uid", "Username", "Email", "Password", "ModifiedDate", "CreatedDate") 
            VALUES (@Uid, @Username, @Email, @Password, @ModifiedDate, @CreatedDate)
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
                        Uid = item.Uid,
                        Username = item.Username,
                        Email = item.Email,
                        Password = item.Password,
                        CreatedDate = now,
                        ModifiedDate = now
                    }, transaction);
                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                var msg = $"Error while trying to save user with uid {item.Uid}";
                throw new Exception(msg, ex);
            }
        }
    }
}
