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

            await conn.ExecuteAsync(new CommandDefinition(Resources.UserInfoTable, cancellationToken: ct));
            await conn.ExecuteAsync(new CommandDefinition(Resources.ChatConversationTable, cancellationToken: ct));
            await conn.ExecuteAsync(new CommandDefinition(Resources.ChatServerTable, cancellationToken: ct));
            await conn.ExecuteAsync(new CommandDefinition(Resources.ChatChannelTable, cancellationToken: ct));
            await conn.ExecuteAsync(new CommandDefinition(Resources.ChatMessageTable, cancellationToken: ct));
            await conn.ExecuteAsync(new CommandDefinition(Resources.MediaFileTable, cancellationToken: ct));
            await conn.ExecuteAsync(new CommandDefinition(Resources.ChatMessageAttachmentTable, cancellationToken: ct));
            await conn.ExecuteAsync(new CommandDefinition(Resources.FriendTable, cancellationToken: ct));
            await conn.ExecuteAsync(new CommandDefinition(Resources.DirectConversationTable, cancellationToken: ct));
        }

        public int Priority => 0;
    }
}
