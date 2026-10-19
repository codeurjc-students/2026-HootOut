using Dapper;
using HootOut.ChatMessages.Searchs;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.Users.Dtos;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;

namespace HootOut.PostgreSQL.Searchs.ChatMessages
{
    public class ChatMessageAttachmentSearch : IChatMessageAttachmentSearch
    {
        private readonly IPersistenceProvider provider;
        public ChatMessageAttachmentSearch(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private const string getAttachmentsByMessageId = """
            SELECT 
                attachment."Id" as Id,
                attachment."MessageId" as MessageId,
                attachment."MediaFileId" as MediaFileId,
                mediaFile."FileName" as FileName,
                mediaFile."ContentType" as ContentType
            FROM "HootOut"."ChatMessageAttachment" AS attachment
            INNER JOIN "HootOut"."MediaFile" AS mediaFile
                ON attachment."MediaFileId" = mediaFile."Id"
            WHERE attachment."MessageId" = @MessageId
            """; 

        public async Task<IEnumerable<ChatMessageAttachmentDto>> GetChatMessageAttachmentDtos(Guid messageId, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            return await conn.QueryAsync<ChatMessageAttachmentDto>(
                new CommandDefinition(getAttachmentsByMessageId, new
                {
                    MessageId = messageId
                }, cancellationToken: ct));
        }
    }
}
