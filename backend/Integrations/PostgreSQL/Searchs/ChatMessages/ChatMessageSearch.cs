using Dapper;
using HootOut.ChatMessages.Searchs;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.Users.Dtos;

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
                chatMessage."Id",
                chatMessage."AuthorId",
                chatMessage."ChatChannelId",
                chatMessage."Content",
                chatMessage."CreatedAt",
                userInfo."Username" as Username
            FROM "HootOut"."ChatMessage" AS chatMessage
            LEFT JOIN "HootOut"."UserInfo" AS userInfo
                ON (chatMessage."AuthorId" = userInfo."Id")
            WHERE chatMessage."ChatChannelId" = @chatChannelId
            ORDER BY chatMessage."CreatedAt" DESC
        """;

        public IEnumerable<ChatMessageDto> GetMessagesByChannelId(Guid channelId)
        {
            using (var conn = persistenceProvider.OpenConnection())
            {
                return conn.Query<ChatMessageSearchDto>(getChatMessagesByChannelIdQuery, new
                {
                    chatChannelId = channelId
                }).Select(x => new ChatMessageDto
                {
                    Id = x.Id,
                    Author = new UserDto
                    {
                        Id = x.AuthorId,
                        Username = x.Username
                    },
                    ChatChannelId = x.ChatChannelId,
                    Content = x.Content,
                    CreatedAt = x.CreatedAt
                }).ToList();
            }
        }
    }

    public sealed record ChatMessageSearchDto
    {
        public Guid Id { get; init; }
        public Guid AuthorId { get; init; }
        public Guid ChatChannelId { get; init; }
        public string? Content { get; init; }
        public string? Username { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
