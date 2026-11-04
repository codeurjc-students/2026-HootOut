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
            return (await conn.QueryAsync<ServerMember?>(new CommandDefinition($"""
                SELECT 
                    {serverMemberColumns}
                FROM "HootOut"."ServerMember" member
                WHERE member."ChatServerId" = @ChatServerId AND member."UserId" = @UserId 
                    AND member."Deleted" = FALSE
                    ORDER BY "ModifiedAt" DESC
                """,
                new { ChatServerId = serverId, UserId = userId },
             cancellationToken: ct)))
             .FirstOrDefault();
        }

        public async Task<PageResult<ServerMemberDto>> GetServerMemberDtosAsync(Guid serverId, PageRequest request, CancellationToken ct = default)
        {
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
            var membersWhere = """ WHERE m."ChatServerId" = @ServerId """;

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
                request.Next,
                Take = request.Size + 1,
            }, cancellationToken: ct));

            var members = (await multi.ReadAsync<ServerMemberDto>()).ToList(); 
            var total = await multi.ReadSingleAsync<int>();

            return PaginatedQueryHelper.ToPage(members, request, x => x.Id, total);
        }
    }
}
