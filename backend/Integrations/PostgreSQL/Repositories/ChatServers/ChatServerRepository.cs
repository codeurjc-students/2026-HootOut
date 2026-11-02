using Dapper;
using HootOut.ChatServers.Entities;
using HootOut.ChatServers.Repositories;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.ChatServers.Enums;
using HootOut.Contracts.ChatServers.Requests;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Saver;

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
            """ server."Id", server."Name", server."Description", server."OwnerId", server."CreatedAt", server."ModifiedAt" """;

        private const string serverMemberColumns =
            """ member."Id", member."ChatServerId", member."UserId", member."InvitationId", member."CreatedAt", member."ModifiedAt" """;
        private const string serverInvitationColumns =
            """ invitation."Id", invitation."ChatServerId", invitation."InvitedBy", invitation."UserId", invitation."Status", invitation."CreatedAt", invitation."ModifiedAt" """;

        public async Task<IEnumerable<ChatServer>> GetServersByUserAsync(Guid userId, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            return await conn.QueryAsync<ChatServer>(new CommandDefinition($"""
                SELECT 
                    {serverColumns}
                FROM "HootOut"."ChatServer" server
                INNER JOIN "HootOut"."ServerMember" member
                    ON server."Id" = member."ChatServerId"
                WHERE member."UserId" = @UserId 
                    AND server."Deleted" = FALSE AND member."Deleted" = FALSE
                ORDER BY server."CreatedAt" DESC
                """,
                new { UserId = userId },
                cancellationToken: ct));
        }

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

        public async Task<Guid> AcceptServerInvitationAsync(ServerInvitation invitaiton, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            const string sql = """
                    WITH accepted AS (
                        UPDATE "HootOut"."ServerInvitation"
                        SET "Status" = @Accepted,
                            "ModifiedAt" = @now
                        WHERE "Id" = @InvitationId
                          AND "UserId" = @UserId
                          AND "Status" = @Pending 
                        RETURNING "Id", "ChatServerId", "UserId"
                    )
                    INSERT INTO "HootOut"."ServerMember" ("Id", "ChatServerId", "UserId", "InvitationId", "CreatedAt", "ModifiedAt")
                    SELECT @NewMemberId, "ChatServerId", "UserId", "Id", @now, @now
                    FROM accepted
                    ON CONFLICT ("ChatServerId", "UserId") DO NOTHING;
                    """;

            var memberId = Guid.CreateVersion7();

            var now = DateTime.UtcNow;
            await conn.ExecuteAsync(
                new CommandDefinition(sql, new
                {
                    Accepted = ServerInvitationStatus.Accepted,
                    Pending = ServerInvitationStatus.Pending,
                    NewMemberId = memberId,
                    ChatServerId = invitaiton.ChatServerId,
                    UserId = invitaiton.UserId,
                    InvitationId = invitaiton.Id,
                    now
                }, cancellationToken: ct));

            return memberId;
        }

        public async Task<PageResult<ServerMemberDto>> GetServerMemberDtosAsync(Guid serverId, Guid? next = null, int pageSize = 10, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            var cursorFiler = next is null 
                ? "" 
                : """
                AND (member."CreatedAt", member."Id") < (
                        SELECT m."CreatedAt", m."Id"
                        FROM "HootOut"."ServerMember" m
                        WHERE m."Id" = @Next)
                """;

            var sql = $"""
                WITH page AS (
                    SELECT 
                        member."Id",
                        member."UserId",
                        member."CreatedAt"
                    FROM "HootOut"."ServerMember" member 
                    WHERE member."ChatServerId" = @ServerId 
                    {cursorFiler}
                    ORDER BY member."CreatedAt" DESC, member."Id" DESC
                    LIMIT @Take
                )
                SELECT
                    p."Id", 
                    p."UserId",
                    p."CreatedAt",
                    u."Username"
                FROM page p
                INNER JOIN "HootOut"."UserInfo" u
                    ON p."UserId" = u."Id"
                ORDER BY p."CreatedAt" DESC, p."Id" DESC;
                """;

            var members = (await conn.QueryAsync<ServerMemberDto>(new CommandDefinition(sql, new
            {
                ServerId = serverId,
                Next = next,
                Take = pageSize + 1
            }))).ToList();

            var hasMore = members.Count > pageSize;
            if (hasMore)
                members.RemoveAt(members.Count - 1);

            var nextCursor = hasMore ? members[^1].Id : (Guid?)null;

            return new PageResult<ServerMemberDto>
            {
                Items = members,
                Next = nextCursor,
                PageSize = pageSize
            };
        }
    }
}
