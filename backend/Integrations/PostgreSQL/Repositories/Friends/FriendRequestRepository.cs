using Dapper;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.ChatConversations.Enums;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Enums;
using HootOut.Contracts.Friends.Dtos;
using HootOut.Friends.Entities;
using HootOut.Friends.Repositories;
using HootOut.PostgreSQL.Helpers;

namespace HootOut.PostgreSQL.Repositories.Friends
{
    public sealed class FriendRequestRepository : IFriendRequestRepository
    {
        IPersistenceProvider provider;

        public FriendRequestRepository(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private const string friendRequestColumns =
            """ m."Id", m."InvitedBy", m."UserId", m."Status", m."CreatedAt", m."ModifiedAt" """;

        private Dictionary<string, string> receivedFriendRequestsSortColumns = new()
        {
            { "createdat", """m."CreatedAt" """ },
            { "status", """ m."Status" """ },
            { "invitedby", """ lower(u."Username") COLLATE "es-x-icu" """ },
        };

        private Dictionary<string, string> sentFriendRequestsSortColumns = new()
        {
            { "createdat", """m."CreatedAt" """ },
            { "status", """ m."Status" """ },
            { "username", """ lower(u."Username") COLLATE "es-x-icu" """ }
        };

        public async Task<FriendRequest?> GetFriendRequestByIdAsync(Guid friendRequestId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return (await conn.QueryAsync<FriendRequest?>(new CommandDefinition($"""
                SELECT 
                    {friendRequestColumns}
                FROM "HootOut"."FriendRequest" m
                WHERE m."Id" = @Id 
                """,
               new { Id = friendRequestId },
            cancellationToken: ct)))
            .FirstOrDefault();
        }

        public async Task<FriendRequest?> GetPendingFriendRequestAsync(Guid userA, Guid userB, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            return await conn.QuerySingleOrDefaultAsync<FriendRequest?>(new CommandDefinition($"""
        SELECT
            {friendRequestColumns}
        FROM "HootOut"."FriendRequest" m
        WHERE LEAST(m."InvitedBy", m."UserId") = LEAST(@UserA, @UserB)
          AND GREATEST(m."InvitedBy", m."UserId") = GREATEST(@UserA, @UserB)
          AND m."Status" = @Pending
        """,
                new { UserA = userA, UserB = userB, Pending = InvitationStatus.Pending },
                cancellationToken: ct));
        }

        public async Task<PageResult<FriendRequestDto>> GetReceivedFriendRequestsAsync(PageRequest request, Guid userId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            if (!receivedFriendRequestsSortColumns.TryGetValue(request.SortBy?.ToLowerInvariant() ?? "", out var sort))
            {
                sort = receivedFriendRequestsSortColumns["createdat"];
            }

            var invitationsFrom = """
                FROM "HootOut"."FriendRequest" m 
                INNER JOIN "HootOut"."UserInfo" u
                    ON m."InvitedBy" = u."Id"
                """;

            var invitationsWhere = """ WHERE m."UserId" = @UserId AND m."Status" = @Pending """;

            var sql = $"""
                SELECT 
                    {friendRequestColumns},
                    u."Username" as InvitedByUsername,
                    u."Id" as InvitedById
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
                Pending = InvitationStatus.Pending,
                request.Next,
                Take = request.Size + 1,
            }, cancellationToken: ct));

            var invitations = (await multi.ReadAsync<FriendRequestDto>()).ToList();
            var total = await multi.ReadSingleAsync<int>();

            return PaginatedQueryHelper.ToPage(invitations, request, x => x.Id, total);
        }

        public async Task<PageResult<FriendRequestDto>> GetSentFriendRequestsAsync(PageRequest request, Guid userId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            if (!sentFriendRequestsSortColumns.TryGetValue(request.SortBy?.ToLowerInvariant() ?? "", out var sort))
            {
                sort = sentFriendRequestsSortColumns["createdat"];
            }

            var invitationsFrom = """
                FROM "HootOut"."FriendRequest" m 
                INNER JOIN "HootOut"."UserInfo" u
                    ON m."UserId" = u."Id"
                """;

            var invitationsWhere = """ WHERE m."InvitedBy" = @UserId AND m."Status" = @Pending """;

            var sql = $"""
                SELECT 
                    {friendRequestColumns},
                    u."Username",
                    u."Id" as UserId
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
                Pending = InvitationStatus.Pending,
                request.Next,
                Take = request.Size + 1,
            }, cancellationToken: ct));

            var invitations = (await multi.ReadAsync<FriendRequestDto>()).ToList();
            var total = await multi.ReadSingleAsync<int>();

            return PaginatedQueryHelper.ToPage(invitations, request, x => x.Id, total);
        }

        public async Task<Guid?> AcceptFriendRequestAsync(FriendRequest friendRequest, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            try
            {
                await using var tx = await conn.BeginTransactionAsync(ct);
                var now = DateTime.UtcNow;

                const string sql = $"""
                    WITH pending AS (
                        SELECT "Id", "UserId", "InvitedBy"
                        FROM "HootOut"."FriendRequest"
                        WHERE "Id" = @FriendRequestId
                          AND "UserId" = @UserId
                          AND "Status" = @Pending
                        FOR UPDATE
                    ),
                    inserted AS (
                        INSERT INTO "HootOut"."FriendRelation" ("Id", "User1Id", "User2Id", "FriendRequestId", "CreatedAt", "ModifiedAt")
                        SELECT @NewRelationId, LEAST("UserId", "InvitedBy"), GREATEST("UserId", "InvitedBy"), "Id", @Now, @Now
                        FROM pending
                        ON CONFLICT ("User1Id", "User2Id") WHERE "Deleted" = FALSE DO NOTHING
                        RETURNING "Id", "FriendRequestId"
                    ),
                    accepted AS (
                        UPDATE "HootOut"."FriendRequest"
                        SET "Status" = @Accepted,
                            "ModifiedAt" = @Now
                        WHERE "Id" IN (SELECT "FriendRequestId" FROM inserted)
                    )
                    SELECT "Id" FROM inserted m;
                """;

                var relationId = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(sql, new
                {
                    Accepted = InvitationStatus.Accepted,
                    Pending = InvitationStatus.Pending,
                    NewRelationId = Guid.CreateVersion7(),
                    UserId = friendRequest.UserId,
                    FriendRequestId = friendRequest.Id,
                    Now = now
                }, tx, cancellationToken: ct));

                if (relationId == null)
                {
                    return null;
                }

                //Create new chat conversation

                // Only the first time this pair becomes friends: if they unfriended and
                // came back, they keep their old conversation and its messages.
                const string conversationSql = """
                    WITH relation AS (
                        SELECT r."User1Id", r."User2Id"
                        FROM "HootOut"."FriendRelation" r
                        WHERE r."Id" = @RelationId
                            AND NOT EXISTS (
                                SELECT 1 FROM "HootOut"."DirectConversation" d
                                WHERE d."User1Id" = r."User1Id" AND d."User2Id" = r."User2Id")
                    ),
                    conversation AS (
                        INSERT INTO "HootOut"."ChatConversation" ("Id", "ConversationType", "CreatedAt", "ModifiedAt")
                        SELECT @ConversationId, @Direct, @Now, @Now
                        FROM relation
                        RETURNING "Id"
                    )
                    INSERT INTO "HootOut"."DirectConversation" ("Id", "ChatConversationId", "User1Id", "User2Id", "CreatedAt", "ModifiedAt")
                    SELECT @DirectConversationId, c."Id", r."User1Id", r."User2Id", @Now, @Now
                    FROM conversation c CROSS JOIN relation r;
                """;

                await conn.ExecuteAsync(new CommandDefinition(conversationSql, new
                {
                    RelationId = relationId,
                    ConversationId = Guid.CreateVersion7(),
                    DirectConversationId = Guid.CreateVersion7(),
                    Direct = ChatConversationType.Direct,
                    Now = now
                }, tx, cancellationToken: ct));

                await tx.CommitAsync(ct);
                return relationId;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var msg = $"Error while accepting the friend request with Id {friendRequest.Id} for user {friendRequest.UserId}";
                throw new Exception(msg, ex);
            }
        }
    }
}
