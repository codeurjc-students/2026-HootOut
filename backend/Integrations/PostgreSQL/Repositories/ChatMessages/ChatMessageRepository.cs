using Dapper;
using HootOut.ChatMessages.Repositories;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Dto;
using HootOut.Contracts.Users.Dtos;

namespace HootOut.PostgreSQL.Repositories.ChatMessages
{
    public class ChatMessageRepository : IChatMessageRepository
    {
        private const int maxPageSize = 50;
        private readonly IPersistenceProvider provider;
        public ChatMessageRepository(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public async Task<PageResult<ChatMessageDto>> SearchMessagesByConversationIdAsync(Guid conversationId, Guid? next = null, int pageSize = 10, CancellationToken ct = default)
        {
            pageSize = Math.Clamp(pageSize, 1, maxPageSize);
            var cursorFilter = next is null
                 ? ""
                 : """
                  AND (m."CreatedAt", m."Id") < (
                      SELECT c."CreatedAt", c."Id"
                      FROM "HootOut"."ChatMessage" c
                      WHERE c."Id" = @Next
                        AND c."ChatConversationId" = @ConversationId)
                  """;

            var sql = $"""
                 WITH page AS (
                    SELECT m."Id", m."CreatedAt", m."ModifiedAt", m."Content",
                           m."ChatConversationId", m."AuthorId"
                    FROM "HootOut"."ChatMessage" m
                    WHERE m."ChatConversationId" = @ConversationId
                      AND m."Deleted" = false
                      {cursorFilter}
                    ORDER BY m."CreatedAt" DESC, m."Id" DESC
                    LIMIT @Take
                )
                SELECT
                    p."Id", p."CreatedAt", p."ModifiedAt", p."Content", p."ChatConversationId",
                    u."Id", u."Username", u."Email",
                    a."Id", a."MessageId", a."MediaFileId", a."CreatedAt",
                    mf."FileName", mf."ContentType",
                    umf."Id" as MediaFileId, umf."FileName", umf."ContentType"
                FROM page p
                LEFT JOIN "HootOut"."UserInfo" u
                       ON u."Id" = p."AuthorId"
                LEFT JOIN "HootOut"."MediaFile" umf
                       ON u."ProfilePictureId" = umf."Id"
                LEFT JOIN ("HootOut"."ChatMessageAttachment" a
                    INNER JOIN "HootOut"."MediaFile" mf
                            ON mf."Id" = a."MediaFileId" AND mf."Deleted" = false)
                ON a."MessageId" = p."Id" AND a."Deleted" = false
                ORDER BY p."CreatedAt" DESC, p."Id" DESC, a."CreatedAt", a."Id";
                """;

            var messages = new List<ChatMessageDto>();
            var lookup = new Dictionary<Guid, ChatMessageDto>();

            await using var conn = await provider.OpenConnectionAsync(ct);

            await conn.QueryAsync<ChatMessageDto, UserDto?, ChatMessageAttachmentDto?, PictureDto?, ChatMessageDto>(
                new CommandDefinition(sql, new
                {
                    ConversationId = conversationId,
                    Next = next,
                    Take = pageSize + 1
                }, cancellationToken: ct),
                (message, author, attachment, profilePicture) =>
                {
                    if (!lookup.TryGetValue(message.Id, out var m))
                    {
                        m = message;
                        m.Author = author;

                        if (m.Author != null && profilePicture!= null)
                        {
                            m.Author.ProfilePicture = profilePicture;
                        }

                        lookup.Add(m.Id, m);
                        messages.Add(m);
                    }

                    if (attachment is not null)
                        m.Attachments.Add(attachment);

                    return m;
                },
                splitOn: "Id,Id,MediaFileId");

            var hasMore = messages.Count > pageSize;
            if (hasMore)
                messages.RemoveAt(messages.Count - 1);

            var nextCursor = hasMore ? messages[^1].Id : (Guid?)null;

            return new PageResult<ChatMessageDto> { Items = messages, Next = nextCursor, PageSize = pageSize };

        }
    }
}
