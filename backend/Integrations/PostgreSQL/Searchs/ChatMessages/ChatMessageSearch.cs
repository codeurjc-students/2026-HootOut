using Dapper;
using HootOut.ChatMessages.Searchs;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.Users.Dtos;

namespace HootOut.PostgreSQL.Searchs.ChatMessages
{
    public class ChatMessageSearch : IChatMessageSearch
    {

        private IPersistenceProvider provider;
        public ChatMessageSearch(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
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

        public async Task<IEnumerable<ChatMessageDto>> GetMessagesByChannelIdAsync(Guid channelId, CancellationToken ct = default)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            return (await conn.QueryAsync<ChatMessageSearchDto>(
                new CommandDefinition(
                    getChatMessagesByChannelIdQuery, new
                    {
                        chatChannelId = channelId
                    }, cancellationToken: ct))
                )
                .Select(x => new ChatMessageDto
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
