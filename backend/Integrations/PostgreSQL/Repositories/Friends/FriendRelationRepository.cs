using Dapper;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Enums;
using HootOut.Contracts.Friends.Dtos;
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

        public async Task<PageResult<FriendRelationDto>> GetUserFriendsAsync(PageRequest request, Guid userId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            if (!receivedFriendRequestsSortColumns.TryGetValue(request.SortBy?.ToLowerInvariant() ?? "", out var sort))
            {
                sort = receivedFriendRequestsSortColumns["createdat"];
            }

            var invitationsFrom = """
                FROM "HootOut"."FriendRelation" m 
                INNER JOIN "HootOut"."UserInfo" u
                    ON m."User1Id" = u."Id" OR m."User2Id" = u."Id"
                INNER JOIN "HootOut"."DirectConversation" dc
                    ON m."User1Id" = dc."User1Id" AND m."User2Id" = dc."User2Id"
                """;

            var invitationsWhere = """ WHERE (m."User1Id" = @UserId OR m."User2Id" = @UserId) AND u."Id" != @UserId """;

            var sql = $"""
                SELECT 
                    {friendRelationColumns},
                    u."Id",
                    u."Username",
                    dc."ChatConversationId"
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

            var friendRelations = multi.Read<FriendRelationDto, UserDto, Guid, FriendRelationDto>((friendRelation, user, conversationId) =>
            {
                friendRelation.Friend = user;
                friendRelation.ChatConversationId = conversationId;
                return friendRelation;
            }, splitOn: "Id,ChatConversationId").ToList();

            var total = await multi.ReadSingleAsync<int>();

            return PaginatedQueryHelper.ToPage(friendRelations, request, x => x.Id, total);
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
                """
                , new
                {
                    ConversationId = chatConversationId,
                    UserId = userId,
                }, cancellationToken: ct));
        }
    }
}
