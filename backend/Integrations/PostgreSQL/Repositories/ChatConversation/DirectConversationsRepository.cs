using Dapper;
using HootOut.ChatConversations.Repositories;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.ChatConversations.Dtos;
using HootOut.Contracts.Common.Dto;
using HootOut.Contracts.Users.Dtos;

namespace HootOut.PostgreSQL.Repositories.ChatConversation
{
    public class DirectConversationsRepository : IDirectConversationsRepository
    {
        private readonly IPersistenceProvider provider;

        public DirectConversationsRepository(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public async Task<DirectConversationDto?> GetDirectConversationByIdAsync(Guid directConversatioId, Guid userId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            var result = await conn.QueryAsync<DirectConversationDto?, UserDto, UserDto, PictureDto?, PictureDto?, DirectConversationDto?>(
                new CommandDefinition("""
                    SELECT 
                        m."Id",
                        m."ChatConversationId",
                        u1."Id",
                        u1."Username",
                        u2."Id",
                        u2."Username",
                        mf1."Id" as MediaFileId,
                        mf1."FileName",
                        mf1."ContentType",
                        mf2."Id" as MediaFileId,
                        mf2."FileName",
                        mf2."ContentType"
                    FROM "HootOut"."DirectConversation" m
                    INNER JOIN "HootOut"."UserInfo" u1
                        ON u1."Id" = m."User1Id"
                    LEFT JOIN "HootOut"."MediaFile" mf1
                        ON u1."ProfilePictureId" = mf1."Id"
                    INNER JOIN "HootOut"."UserInfo" u2
                        ON u2."Id" = m."User2Id"
                    LEFT JOIN "HootOut"."MediaFile" mf2
                        ON u2."ProfilePictureId" = mf2."Id"
                WHERE m."Id" = @DirectConversationId AND (u1."Id" = @UserId OR u2."Id" = @UserId)
                """,
                new { DirectConversationId = directConversatioId, userId = userId },
                cancellationToken: ct), (directConversation, user1, user2, picture1, picture2) =>
                {
                    if (directConversation != null)
                    {
                        directConversation.User1 = user1;
                        directConversation.User2 = user2;

                        if (picture1 != null)
                        {
                            user1.ProfilePicture = picture1;
                        }

                        if (picture2 != null)
                        {
                            user2.ProfilePicture = picture2;
                        }
                    }
                    return directConversation;
                }, splitOn: "Id,Id,Id,MediaFileId,MediaFileId");

            return result.FirstOrDefault();
        }
    }
}
