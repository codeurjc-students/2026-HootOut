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

        public async Task<IEnumerable<ChatChannel>> GetChannelsByServerId(Guid serverId, CancellationToken ct = default)
        {
            await using var conn = await persistenceProvider.OpenConnectionAsync(ct);

            return await conn.QueryAsync<ChatChannel>(new CommandDefinition(
               $"""
                    SELECT
                        {ChatChannelColums}
                    FROM "HootOut"."ChatChannel"
                    WHERE "ChatServerId" = @ServerId AND "Deleted" = False
                    ORDER BY "Name"
                """,
                new { ServerId = serverId },
                cancellationToken: ct));
        }

        public async Task<ChatChannel?> GetChannelByIdAsync(Guid channelId, CancellationToken ct = default)
        {
            await using var conn = await persistenceProvider.OpenConnectionAsync(ct);

            return (await conn.QueryAsync<ChatChannel>(new CommandDefinition(
               $"""
                    SELECT
                        {ChatChannelColums}
                    FROM "HootOut"."ChatChannel"
                    WHERE "Id" = @Id AND "Deleted" = False
                    ORDER BY "Name"
                """,
                new { Id = channelId },
                cancellationToken: ct))).FirstOrDefault();
        }
    }
}
