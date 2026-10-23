using Dapper;
using HootOut.ChatMessages.Searchs;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.Common;
using HootOut.Contracts.Users.Dtos;
using System.Data.Common;

namespace HootOut.PostgreSQL.Searchs.ChatMessages
{
    public class ChatMessageSearch : IChatMessageSearch
    {
        private const int maxPageSize = 50;
        private readonly IPersistenceProvider provider;
        public ChatMessageSearch(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        } 

        public async Task<PageResult<ChatMessageDto>> SearchMessagesByChannelIdAsync(Guid channelId, Guid? next = null, int pageSize = 10, CancellationToken ct = default)
        {
            pageSize = Math.Clamp(pageSize, 1, maxPageSize);
            var cursorFilter = next is null
                ? ""
                : """
                  AND (m."CreatedAt", m."Id") < (
                      SELECT c."CreatedAt", c."Id"
                      FROM "HootOut"."ChatMessage" c
                      WHERE c."Id" = @Next)
                  """;

            var sql = $"""
                 WITH page AS (
                    SELECT m."Id", m."CreatedAt", m."ModifiedAt", m."Content",
                           m."ChatChannelId", m."AuthorId"
                    FROM "HootOut"."ChatMessage" m
                    WHERE m."ChatChannelId" = @ChannelId
                      AND m."Deleted" = false
                      {cursorFilter}
                    ORDER BY m."CreatedAt" DESC, m."Id" DESC
                    LIMIT @Take
                )
                SELECT
                    p."Id", p."CreatedAt", p."ModifiedAt", p."Content", p."ChatChannelId",
                    u."Id", u."Username", u."Email",
                    a."Id", a."MessageId", a."MediaFileId", a."CreatedAt",
                    mf."FileName", mf."ContentType"
                FROM page p
                LEFT JOIN "HootOut"."UserInfo" u
                       ON u."Id" = p."AuthorId"
                LEFT JOIN "HootOut"."ChatMessageAttachment" a
                       ON a."MessageId" = p."Id" AND a."Deleted" = false
                LEFT JOIN "HootOut"."MediaFile" mf
                       ON mf."Id" = a."MediaFileId" AND a."Deleted" = false
                ORDER BY p."CreatedAt" DESC, p."Id" DESC, a."CreatedAt";
                """;

            var messages = new List<ChatMessageDto>();
            var lookup = new Dictionary<Guid, ChatMessageDto>();

            await using var conn = await provider.OpenConnectionAsync(ct);

            await conn.QueryAsync<ChatMessageDto, UserDto?, ChatMessageAttachmentDto?, ChatMessageDto>(
                new CommandDefinition(sql, new
                {
                    ChannelId = channelId,
                    Next = next,
                    Take = pageSize + 1          
                }, cancellationToken: ct),
                (message, author, attachment) =>
                {
                    if (!lookup.TryGetValue(message.Id, out var m))
                    {
                        m = message;
                        m.Author = author;
                        lookup.Add(m.Id, m);
                        messages.Add(m);
                    }

                    if (attachment is not null)
                        m.Attachments.Add(attachment);

                    return m;
                },
                splitOn: "Id,Id");

            var hasMore = messages.Count > pageSize;
            if (hasMore)
                messages.RemoveAt(messages.Count - 1);

            var nextCursor = hasMore ? messages[^1].Id : (Guid?)null;

            return new PageResult<ChatMessageDto> { Items =  messages, Next = nextCursor, PageSize = pageSize };

        }
    } 
}
