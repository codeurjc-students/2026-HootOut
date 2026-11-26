using Dapper;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Dto;
using HootOut.Contracts.Common.Enums;
using HootOut.Contracts.Friends.Responses;
using HootOut.Contracts.Users.Dtos;
using HootOut.Friends.Repositories;
using HootOut.PostgreSQL.Helpers;

namespace HootOut.PostgreSQL.Repositories.Friends
{
    public sealed class FriendRelationRepository : IFriendRelationRepository
    {
        private readonly IPersistenceProvider provider;

        public FriendRelationRepository(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private const string friendRelationColumns =
            """ m."Id", m."User1Id", m."User2Id", m."FriendRequestId", m."CreatedAt", m."ModifiedAt" """;

        private Dictionary<string, string> receivedFriendRequestsSortColumns = new()
        {
            { "createdat", """m."CreatedAt" """ },
            { "username", """ lower(u."Username") COLLATE "es-x-icu" """ },
        };

        public async Task<PageResult<UserFriendResponse>> GetUserFriendsAsync(PageRequest request, Guid userId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            if (!receivedFriendRequestsSortColumns.TryGetValue(request.SortBy?.ToLowerInvariant() ?? "", out var sort))
            {
                sort = receivedFriendRequestsSortColumns["createdat"];
            }

            var invitationsFrom = """
                FROM "HootOut"."FriendRelation" m 
                INNER JOIN "HootOut"."UserInfo" u
                    ON u."Id" = CASE WHEN m."User1Id" = @UserId THEN m."User2Id" ELSE m."User1Id" END
                INNER JOIN "HootOut"."DirectConversation" dc
                    ON m."User1Id" = dc."User1Id" AND m."User2Id" = dc."User2Id"
                LEFT JOIN "HootOut"."MediaFile" mf
                    ON u."ProfilePictureId" = mf."Id"
                """;

            var invitationsWhere = """ WHERE (m."User1Id" = @UserId OR m."User2Id" = @UserId) AND m."Deleted" = FALSE """;

            var sql = $"""
                SELECT  
                    m."Id" as "FriendRelationId",
                    dc."Id" as DirectConversationId,
                    dc."ChatConversationId",
                    u."Id",
                    u."Username",
                    mf."Id" as MediaFileId,
                    mf."FileName",
                    mf."ContentType"
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

            var friendRelations = multi.Read<UserFriendResponse, UserDto, PictureDto?, UserFriendResponse>((friendRelation, user, picture) =>
            {
                friendRelation.Friend = user;
                if (picture != null)
                {
                    user.ProfilePicture = picture;
                }
                return friendRelation;
            }, splitOn: "Id,MediaFileId").ToList();

            var total = await multi.ReadSingleAsync<int>();

            return PaginatedQueryHelper.ToPage(friendRelations, request, x => x.FriendRelationId, total);
        }

        public async Task<Guid?> GetDirectConverationByConversationAndUserId(Guid chatConversationId, Guid userId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            return await conn.QueryFirstOrDefaultAsync<Guid?>(new CommandDefinition("""
                WITH dc AS (
                    SELECT dc."Id", dc."User1Id", dc."User2Id"
                    FROM "HootOut"."ChatConversation" conversation
                    INNER JOIN "HootOut"."DirectConversation" dc
                        ON conversation."Id" = dc."ChatConversationId"
                    WHERE conversation."Id" = @ConversationId AND (dc."User1Id" = @UserId OR dc."User2Id" = @UserId )
                )
                SELECT 
                    dc."Id"
                FROM "HootOut"."FriendRelation" fr
                INNER JOIN dc
                    ON fr."User1Id" = dc."User1Id" AND fr."User2Id" = dc."User2Id" 
                WHERE fr."Deleted" = FALSE
                """
                , new
                {
                    ConversationId = chatConversationId,
                    UserId = userId,
                }, cancellationToken: ct));
        }

        public async Task<bool> AreFriendsAsync(Guid userA, Guid userB, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            // The pair is always stored with the smaller id first, so the order is computed in SQL
            return await conn.ExecuteScalarAsync<bool>(new CommandDefinition("""
        SELECT EXISTS (
            SELECT 1
            FROM "HootOut"."FriendRelation"
            WHERE "User1Id" = LEAST(@UserA, @UserB)
              AND "User2Id" = GREATEST(@UserA, @UserB)
              AND "Deleted" = FALSE)
        """,
                new { UserA = userA, UserB = userB },
                cancellationToken: ct));
        }

        public async Task<IEnumerable<Guid>> GetFriendIdsAsync(Guid userId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            return await conn.QueryAsync<Guid>(new CommandDefinition("""
            SELECT CASE WHEN "User1Id" = @UserId THEN "User2Id" ELSE "User1Id" END
            FROM "HootOut"."FriendRelation"
            WHERE ("User1Id" = @UserId OR "User2Id" = @UserId)
              AND "Deleted" = FALSE
            """,
                new { UserId = userId },
                cancellationToken: ct));
        }
    }
}
