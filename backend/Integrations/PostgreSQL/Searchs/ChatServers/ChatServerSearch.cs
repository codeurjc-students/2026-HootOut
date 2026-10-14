using Dapper;
using HootOut.ChatServers.ChatServers;
using HootOut.ChatServers.Searchs;
using HootOut.CommonDomain.Persistence;

namespace HootOut.PostgreSQL.Searchs.ChatServers
{
    public sealed class ChatServerSearch : IChatServerSearch
    {
        private IPersistenceProvider provider;

        public ChatServerSearch(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public async Task<IEnumerable<ChatServer>> GetAllChatServersAsync(CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            return await conn.QueryAsync<ChatServer>("""
                    SELECT 
                    "Id",
                    "Name",
                    "Description",
                    "OwnerId",
                    "CreatedAt",
                    "ModifiedAt"
                FROM "HootOut"."ChatServer"
             """, ct);
        }
    }
}
