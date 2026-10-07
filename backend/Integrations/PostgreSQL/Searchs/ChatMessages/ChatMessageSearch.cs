using Dapper;
using HootOut.ChatMessages.ChatMessages;
using HootOut.ChatMessages.Searchs;
using HootOut.CommonDomain.Persistence;

namespace HootOut.PostgreSQL.Searchs.ChatMessages
{
    public class ChatMessageSearch : IChatMessageSearch
    {

        private IPersistenceProvider persistenceProvider;
        public ChatMessageSearch(IPersistenceProvider persistenceProvider)
        {
            this.persistenceProvider = persistenceProvider ?? throw new ArgumentNullException(nameof(persistenceProvider));
        }

        private const string getChatMessagesByChannelIdQuery = """
            SELECT 
                "Id",
                "AuthorId",
                "ChatChannelId",
                "Content",
                "CreatedAt",
                "ModifiedAt"
            FROM "HootOut"."ChatMessage" chatMessage
            WHERE chatMessage."ChatChannelId" = @chatChannelId
            ORDER BY chatMessage."CreatedAt" DESC
        """;

        public IEnumerable<ChatMessage> GetMessagesByChannelId(Guid channelId)
        {
            using (var conn = persistenceProvider.OpenConnection())
            {
                return conn.Query<ChatMessage>(getChatMessagesByChannelIdQuery, new
                {
                    chatChannelId = channelId
                });
            }
        }
    }
}
