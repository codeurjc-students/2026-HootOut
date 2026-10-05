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

        public void Save(ChatMessage item)
        {
            try
            {
                using (var conn = provider.GetNewConnection())
                {
                    var transaction = conn.BeginTransaction();

                    conn.Execute(addChatMessage, new
                    {
                        item.Id,
                        item.AuthorId,
                        item.ChatChannelId,
                        item.Content,
                        item.CreatedAt,
                        item.ModifiedAt

                    }, transaction);
                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                var msg = $"Error while trying to save the chat message {item.Id}";
                throw new Exception(msg, ex);
            }
        }
    }
}
