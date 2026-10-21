using Dapper;
using HootOut.ChatMessages.Entities;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common.Saver;

namespace HootOut.PostgreSQL.Savers.ChatMessages
{
    public class ChatMessageAttachmentSaver : ISaver<ChatMessageAttachment>
    {
        private IPersistenceProvider provider;

        public ChatMessageAttachmentSaver(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private const string addChatMessageAttachments = """
            INSERT INTO "HootOut"."ChatMessageAttachment" ("Id", "MessageId", "MediaFileId", "CreatedAt", "ModifiedAt") 
            VALUES (@Id, @MessageId, @MediaFileId, @CreatedAt, @ModifiedAt)
        """;

        public async Task SaveManyAsync(IEnumerable<ChatMessageAttachment> items, CancellationToken ct = default)
        {
            if (items == null || !items.Any())
                return;

            await using var conn = await provider.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);
            try
            {
                await conn.ExecuteAsync(
                    new CommandDefinition(addChatMessageAttachments, items, tx, cancellationToken: ct));
                await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                var msg = $"Error while trying to save message attachments of message {items.FirstOrDefault()?.MessageId}";
                throw new Exception(msg, ex);
            }

        }
    }
}
