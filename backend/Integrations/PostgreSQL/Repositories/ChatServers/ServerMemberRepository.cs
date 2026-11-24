using Dapper;
using HootOut.ChatServers.Entities;
using HootOut.ChatServers.Repositories;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.Common;
using HootOut.PostgreSQL.Helpers;

namespace HootOut.PostgreSQL.Repositories.ChatServers
{
    public class ServerMemberRepository : IServerMemberRepository
    {
        private IPersistenceProvider provider;

        public ServerMemberRepository(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private const string serverMemberColumns =
    """ member."Id", member."ChatServerId", member."UserId", member."InvitationId", member."CreatedAt", member."ModifiedAt" """;

        private Dictionary<string, string> sortColumns = new()
        {
            { "createdat", """ m."CreatedAt" """ },
            { "username", """ lower(u."Username") COLLATE "es-x-icu" """ }
        };

        public async Task<ServerMember?> GetServerMemberAsync(Guid serverId, Guid userId, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return (await conn.QuerySingleOrDefaultAsync<ServerMember?>(new CommandDefinition($"""
                SELECT 
                    {serverMemberColumns}
                FROM "HootOut"."ServerMember" member
                WHERE member."ChatServerId" = @ChatServerId AND member."UserId" = @UserId 
                    AND member."Deleted" = FALSE 
                """,
                new { ChatServerId = serverId, UserId = userId },
             cancellationToken: ct)));
        }

        public Task<PageResult<ServerMemberDto>> GetServerMemberDtosAsync(Guid serverId, PageRequest request, CancellationToken ct = default)
        {
            return GetServerMemberDtosAsync(serverId, request, [], ct);
        }

        public async Task<PageResult<ServerMemberDto>> GetServerMemberDtosAsync(Guid serverId, PageRequest request, IReadOnlyCollection<Guid> excludedUserIds, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(excludedUserIds);

            await using var conn = await provider.OpenConnectionAsync(ct);

            if (!sortColumns.TryGetValue(request.SortBy?.ToLowerInvariant() ?? "", out var sort))
            {
                sort = sortColumns["createdat"];
            }

            var membersFrom = """
                FROM "HootOut"."ServerMember" m 
                INNER JOIN "HootOut"."UserInfo" u
                    ON m."UserId" = u."Id"
                """;
            // "<> ALL" of an empty array is true: without exclusions it is the same query as before.
            // Npgsql sends the array as one uuid[] parameter (Dapper does not expand it)
            var membersWhere = """
                WHERE m."ChatServerId" = @ServerId AND m."Deleted" = FALSE
                    AND m."UserId" <> ALL(@ExcludedUserIds)
                """;

            var sql = $"""
                SELECT
                    m."Id",
                    m."UserId",
                    m."CreatedAt",
                    u."Username"
                {membersFrom}
                {membersWhere}
                {PaginatedQueryHelper.CursorFilter(membersFrom, sort, request)} 
                {PaginatedQueryHelper.OrderBy(sort, request)}
                LIMIT @Take;

                SELECT COUNT(*)
                {membersFrom}
                {membersWhere};
                """;

            using var multi = await conn.QueryMultipleAsync(new CommandDefinition(sql, new
            {
                ServerId = serverId,
                ExcludedUserIds = excludedUserIds.ToArray(),
                request.Next,
                Take = request.Size + 1,
            }, cancellationToken: ct));

            var members = (await multi.ReadAsync<ServerMemberDto>()).ToList();
            var total = await multi.ReadSingleAsync<int>();

            return PaginatedQueryHelper.ToPage(members, request, x => x.Id, total);
        }

        public async Task<IReadOnlyList<ServerMemberDto>> GetServerMemberDtosByUserIdsAsync(Guid serverId, IReadOnlyCollection<Guid> userIds, int limit, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(userIds);

            if (userIds.Count == 0 || limit <= 0)
                return [];

            await using var conn = await provider.OpenConnectionAsync(ct);

            // Same order as the paginated list sorted by username, with the id as tie breaker
            var members = await conn.QueryAsync<ServerMemberDto>(new CommandDefinition($"""
                SELECT
                    m."Id",
                    m."UserId",
                    m."CreatedAt",
                    u."Username"
                FROM "HootOut"."ServerMember" m
                INNER JOIN "HootOut"."UserInfo" u
                    ON m."UserId" = u."Id"
                WHERE m."ChatServerId" = @ServerId AND m."Deleted" = FALSE
                    AND m."UserId" = ANY(@UserIds)
                ORDER BY {sortColumns["username"]}, m."Id"
                LIMIT @Limit
                """,
                new { ServerId = serverId, UserIds = userIds.ToArray(), Limit = limit },
                cancellationToken: ct));

            return members.AsList();
        }

        public async Task<IEnumerable<Guid>> GetServerIdsByUserIdAsync(Guid userId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            var ids = await conn.QueryAsync<Guid>(new CommandDefinition("""
                SELECT "ChatServerId"
                FROM "HootOut"."ServerMember"
                WHERE "UserId" = @UserId
                  AND "Deleted" = FALSE
                """,
                new { UserId = userId },
                cancellationToken: ct));

            return ids.AsList();
        }

        public async Task<IEnumerable<Guid>> GetServerMemberIdsAsync(Guid serverId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            var ids = await conn.QueryAsync<Guid>(new CommandDefinition("""
                SELECT "UserId"
                FROM "HootOut"."ServerMember"
                WHERE "ChatServerId" = @ServerId
                  AND "Deleted" = FALSE
                """,
           new { ServerId = serverId },
           cancellationToken: ct));

            return ids.AsList();
        }
    }
}
