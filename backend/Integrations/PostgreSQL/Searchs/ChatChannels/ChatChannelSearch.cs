using Dapper;
using HootOut.ChatChannels.ChatChannels;
using HootOut.ChatChannels.Search;
using HootOut.CommonDomain.Persistence;

namespace HootOut.PostgreSQL.Searchs.ChatChannels
{
    public class ChatChannelSearch : IChatChannelSearch
    {
        private IPersistenceProvider persistenceProvider;
        public ChatChannelSearch(IPersistenceProvider persistenceProvider)
        {
            this.persistenceProvider = persistenceProvider ?? throw new ArgumentNullException(nameof(persistenceProvider));
        }

        private const string ChatChannelColums =
            """ "Id", "Name", "ChatServerId", "CreatedAt", "ModifiedAt" """;

        private const string getAllChannelsQuery = $"""
            SELECT {ChatChannelColums}
            FROM "HootOut"."ChatChannel"
        """;

        public async Task<IEnumerable<ChatChannel>> getAllChannelsAsync(CancellationToken ct = default)
        {
            await using var conn = await persistenceProvider.OpenConnectionAsync(ct);

            return await conn.QueryAsync<ChatChannel>(getAllChannelsQuery, ct);
        }

        public async Task<IEnumerable<ChatChannel>> getChannelsByServerId(Guid serverId, CancellationToken ct = default)
        {
            await using var conn = await persistenceProvider.OpenConnectionAsync(ct);

            return await conn.QueryAsync<ChatChannel>(new CommandDefinition(
               $"""
                    SELECT
                        {ChatChannelColums}
                    FROM "HootOut"."ChatChannel"
                    WHERE "ChatServerId" = @ServerId
                    ORDER BY "Name"
                """,
                new { ServerId = serverId },
                cancellationToken: ct));
        }
    }
}
