using Dapper;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.PostgreSQL.Properties;

namespace HootOut.PostgreSQL.DBScripts
{
    public class DBInit : IDefaultValues
    {
        private IPersistenceProvider sqlProvider;

        public DBInit(IPersistenceProvider sqlProvider)
        {
            this.sqlProvider = sqlProvider ?? throw new ArgumentNullException(nameof(sqlProvider));
        }

        public async Task InitAsync(CancellationToken ct = default)
        {
            await using var conn = await sqlProvider.OpenConnectionAsync(ct);

            await conn.ExecuteAsync(Resources.UserInfoTable, ct);
            await conn.ExecuteAsync(Resources.ChatServerTable, ct);
            await conn.ExecuteAsync(Resources.ChatChannelTable, ct);
            await conn.ExecuteAsync(Resources.ChatMessageTable, ct);
        }

        public int Priority => 0;
    }
}
