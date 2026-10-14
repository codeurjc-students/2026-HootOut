using Dapper;
using HootOut.ChatMessages.ChatMessages;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.Common.Saver;

namespace HootOut.PostgreSQL.Savers.ChatMessages
{
    public class ChatMessageSaver : ISaver<ChatMessage>
    {
        private IPersistenceProvider provider;

        public ChatMessageSaver(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private const string addChatMessage = """
            INSERT INTO "HootOut"."ChatMessage" ("Id", "AuthorId", "ChatChannelId", "Content", "CreatedAt", "ModifiedAt") 
            VALUES (@Id, @AuthorId, @ChatChannelId, @Content, @CreatedAt, @ModifiedAt)
        """;

        public async Task SaveAsync(ChatMessage item, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);
            try
            {
                await conn.ExecuteAsync(
                    new CommandDefinition(addChatMessage, new
                    {
                        item.Id,
                        item.AuthorId,
                        item.ChatChannelId,
                        item.Content,
                        item.CreatedAt,
                        item.ModifiedAt

                    }, tx, cancellationToken: ct));
                await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                var msg = $"Error while trying to save the chat message {item.Id}";
                throw new Exception(msg, ex);
            }
        }
    }
}
