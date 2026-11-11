using Dapper;
using HootOut.ChatServers.Entities;
using HootOut.ChatServers.Repositories;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.ChatServers.Enums;
using HootOut.Contracts.Common;
using HootOut.PostgreSQL.Helpers;

namespace HootOut.PostgreSQL.Repositories.ChatServers
{
    public class ServerInvitationRepository : IServerInvitationRepository
    {
        IPersistenceProvider provider;

        public ServerInvitationRepository(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private const string serverInvitationColumns =
            """ invitation."Id", invitation."ChatServerId", invitation."InvitedBy", invitation."UserId", invitation."Status", invitation."CreatedAt", invitation."ModifiedAt" """;

        private Dictionary<string, string> serverInvitationsSortColumns = new()
        {
            { "createdat", """m."CreatedAt" """ },
            { "status", """ m."Status" """ },
            { "username", """ lower(u."Username") COLLATE "es-x-icu" """ }
        };

        private Dictionary<string, string> userInvitationsSortColumns = new()
        {
            { "createdat", """m."CreatedAt" """ },
            { "status", """ m."Status" """ },
            { "servername", """ lower(s."Name") COLLATE "es-x-icu" """ },
            { "invitedby", """ lower(u."Username") COLLATE "es-x-icu" """ }
        };

        public async Task<PageResult<ServerInvitationDto>> GetServerInvitationDtosAsync(Guid serverId, PageRequest request, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            if (!serverInvitationsSortColumns.TryGetValue(request.SortBy?.ToLowerInvariant() ?? "", out var sort))
            {
                sort = serverInvitationsSortColumns["createdat"];
            }

            var invitationsFrom = """
                FROM "HootOut"."ServerInvitation" m 
                INNER JOIN "HootOut"."UserInfo" u
                    ON m."UserId" = u."Id"
                INNER JOIN "HootOut"."ChatServer" s 
                    ON s."Id" = m."ChatServerId" 
                """;

            var invitationsWhere = """ WHERE m."ChatServerId" = @ServerId """;

            var sql = $"""
                SELECT 
                    m."Id",
                    m."UserId",
                    m."Status",
                    m."ChatServerId"
                    u."Username",
                    s."Name" as ServerName,
                    m."CreatedAt",
                    m."ModifiedAt"
                    {invitationsFrom}
                    {invitationsWhere}
                    {PaginatedQueryHelper.CursorFilter(invitationsFrom, sort, request)}
                    {PaginatedQueryHelper.OrderBy(sort, request)}
                    LIMIT @Take;

                    SELECT COUNT(*)
                    {invitationsFrom}
                    {invitationsWhere};
                """;

            using var multi = await conn.QueryMultipleAsync(new CommandDefinition(sql, new
            {
                ServerId = serverId,
                request.Next,
                Take = request.Size + 1,
            }, cancellationToken: ct));

            var invitations = (await multi.ReadAsync<ServerInvitationDto>()).ToList();
            var total = await multi.ReadSingleAsync<int>();

            return PaginatedQueryHelper.ToPage(invitations, request, x => x.Id, total);
        }

        public async Task<PageResult<ServerInvitationDto>> GetUserServerInvitationDtosAsync(Guid userId, PageRequest request, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            if (!userInvitationsSortColumns.TryGetValue(request.SortBy?.ToLowerInvariant() ?? "", out var sort))
            {
                sort = userInvitationsSortColumns["createdat"];
            }

            var invitationsFrom = """
                FROM "HootOut"."ServerInvitation" m 
                INNER JOIN "HootOut"."UserInfo" u
                    ON m."InvitedBy" = u."Id"
                INNER JOIN "HootOut"."ChatServer" s 
                    ON s."Id" = m."ChatServerId"
                """;

            var invitationsWhere = """ WHERE m."UserId" = @UserId AND m."Status" = @Pending """;

            var sql = $"""
                SELECT 
                    m."Id",
                    m."UserId",
                    m."Status",
                    m."ChatServerId"
                    u."Username",
                    s."Name" as ServerName,
                    m."CreatedAt",
                    m."ModifiedAt"
                    {invitationsFrom}
                    {invitationsWhere}
                    {PaginatedQueryHelper.CursorFilter(invitationsFrom, sort, request)}
                    {PaginatedQueryHelper.OrderBy(sort, request)}
                    LIMIT @Take;

                    SELECT COUNT(*)
                    {invitationsFrom}
                    {invitationsWhere};
                """;

            using var multi = await conn.QueryMultipleAsync(new CommandDefinition(sql, new
            {
                UserId = userId,
                Pending = ServerInvitationStatus.Pending,
                request.Next,
                Take = request.Size + 1,
            }, cancellationToken: ct));

            var invitations = (await multi.ReadAsync<ServerInvitationDto>()).ToList();
            var total = await multi.ReadSingleAsync<int>();

            return PaginatedQueryHelper.ToPage(invitations, request, x => x.Id, total);
        }

        public async Task<ServerInvitation?> GetServerInvitationByIdAsync(Guid invitationId, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return (await conn.QueryAsync<ServerInvitation?>(new CommandDefinition($"""
                SELECT 
                    {serverInvitationColumns}
                FROM "HootOut"."ServerInvitation" invitation
                WHERE invitation."Id" = @Id 
                """,
               new { Id = invitationId },
            cancellationToken: ct)))
            .FirstOrDefault();
        }

        public async Task<Guid?> AcceptServerInvitationAsync(ServerInvitation invitation, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
             
            const string sql = """
                WITH pending AS (
                    SELECT "Id", "ChatServerId", "UserId"
                    FROM "HootOut"."ServerInvitation"
                    WHERE "Id" = @InvitationId
                      AND "UserId" = @UserId
                      AND "Status" = @Pending
                    FOR UPDATE
                ),
                inserted AS (
                    INSERT INTO "HootOut"."ServerMember" ("Id", "ChatServerId", "UserId", "InvitationId", "CreatedAt", "ModifiedAt")
                    SELECT @NewMemberId, "ChatServerId", "UserId", "Id", @Now, @Now
                    FROM pending
                    ON CONFLICT ("ChatServerId", "UserId") WHERE "Deleted" = FALSE DO NOTHING
                    RETURNING "Id", "InvitationId"
                ),
                accepted AS (
                    UPDATE "HootOut"."ServerInvitation"
                    SET "Status" = @Accepted,
                        "ModifiedAt" = @Now
                    WHERE "Id" IN (SELECT "InvitationId" FROM inserted)
                )
                SELECT "Id" FROM inserted;
        """;

            return await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(sql, new
            {
                Accepted = ServerInvitationStatus.Accepted,
                Pending = ServerInvitationStatus.Pending,
                NewMemberId = Guid.CreateVersion7(),
                UserId = invitation.UserId,
                InvitationId = invitation.Id,
                Now = DateTime.UtcNow
            }, cancellationToken: ct));
        }
    }
}
