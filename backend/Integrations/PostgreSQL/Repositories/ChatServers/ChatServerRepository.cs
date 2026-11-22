using Dapper;
using HootOut.ChatServers.Repositories;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.ChatChannels.Dtos;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.Common;
using HootOut.PostgreSQL.Helpers;

namespace HootOut.PostgreSQL.Repositories.ChatServers
{
    public sealed class ChatServerRepository : IChatServerRepository
    {
        private IPersistenceProvider provider;

        public ChatServerRepository(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }
        private const string serverColumns =
            """ m."Id", m."Name", m."Description", m."OwnerId", m."CreatedAt", m."ModifiedAt" """;

        private Dictionary<string, string> sortColumns = new()
        {
            { "createdat", """ m."CreatedAt" """ },
            { "name", """ lower(m."Name") COLLATE "es-x-icu" """ }
        };

        public async Task<PageResult<ChatServerDto>> GetServerDtosByUserIdAsync(PageRequest request, Guid userId, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            if (!sortColumns.TryGetValue(request.SortBy?.ToLowerInvariant() ?? "", out var sort))
            {
                sort = sortColumns["createdat"];
            }

            var serversFrom = """
                FROM "HootOut"."ChatServer" m
                INNER JOIN "HootOut"."ServerMember" member
                    ON m."Id" = member."ChatServerId" AND member."UserId" = @UserId AND member."Deleted" = FALSE
                """;

            var serversWhere = """ WHERE m."Deleted" = FALSE """;

            var sql =
            $"""
                SELECT
                {serverColumns}
                {serversFrom}
                {serversWhere}
                {PaginatedQueryHelper.CursorFilter(serversFrom, sort, request)}
                {PaginatedQueryHelper.OrderBy(sort, request)}
                LIMIT @TAKE;

                SELECT COUNT(*)
                {serversFrom}
                {serversWhere};
            """;

            using var multi = await conn.QueryMultipleAsync(new CommandDefinition(sql, new
            {
                UserId = userId,
                request.Next,
                Take = request.Size + 1,
            }, cancellationToken: ct));

            var members = (await multi.ReadAsync<ChatServerDto>()).ToList();
            var total = await multi.ReadSingleAsync<int>();

            return PaginatedQueryHelper.ToPage(members, request, x => x.Id, total);
        }

        public async Task<ChatServerDto?> GetServerDtoByIdAsync(Guid serverId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            var sql = $"""
                SELECT 
                    {serverColumns},
                    channel."Id", 
                    channel."ChatServerId",
                    channel."Name",
                    channel."ChatConversationId"
                FROM "HootOut"."ChatServer" m
                LEFT JOIN "HootOut"."ChatChannel" channel
                    ON m."Id" = channel."ChatServerId" AND channel."Deleted" = FALSE
                WHERE m."Id" = @ServerId AND m."Deleted" = FALSE 
                ORDER BY channel."Name"
                """;

            ChatServerDto? result = null;

            await conn.QueryAsync<ChatServerDto?, ChatChannelDto?, ChatServerDto?>(new CommandDefinition(sql, new
            {
                ServerId = serverId
            }, cancellationToken: ct),
            (server, channel) =>
            {
                if (server == null)
                    return null;

                if (result == null)
                {
                    result = server;
                }

                if (channel != null)
                    result.Channels.Add(channel);
                return server;
            }, splitOn: "Id");

            return result;
        }

        public async Task<Guid?> GetServerIdByConversationAndUserIdAsync(Guid conversationId, Guid userId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            return await conn.QueryFirstOrDefaultAsync<Guid?>(new CommandDefinition("""
        WITH channel AS (
            SELECT channel."Id", channel."ChatServerId"
            FROM "HootOut"."ChatConversation" conversation
            INNER JOIN "HootOut"."ChatChannel" channel
                ON conversation."Id" = channel."ChatConversationId"
            WHERE conversation."Id" = @ConversationId
              AND channel."Deleted" = FALSE
        )
        SELECT 
            s."Id"
        FROM "HootOut"."ChatServer" s
        INNER JOIN "HootOut"."ServerMember" m
            ON s."Id" = m."ChatServerId" AND m."UserId" = @UserId AND m."Deleted" = FALSE
        INNER JOIN channel
            ON s."Id" = channel."ChatServerId"
        WHERE s."Deleted" = FALSE
        """
                , new
                {
                    ConversationId = conversationId,
                    UserId = userId,
                }, cancellationToken: ct));
        }
    }
}
