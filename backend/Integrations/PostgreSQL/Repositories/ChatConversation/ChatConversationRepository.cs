using Dapper;
using HootOut.ChatConversations.Repositories;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.ChatConversations.Dtos;

namespace HootOut.PostgreSQL.Repositories.ChatConversation
{
    public class ChatConversationRepository : IChatConversationsRepository
    {
        private IPersistenceProvider provider;

        public ChatConversationRepository(IPersistenceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private string chatConversationColumns = """
            m."Id", m."ConversationType", m."CreatedAt", m."ModifiedAt"
            """;

        public async Task<ChatConversationDto?> GetChatConversationByIdAsync(Guid conversationId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);

            return await conn.QueryFirstOrDefaultAsync<ChatConversationDto?>(
                new CommandDefinition($"""
                    SELECT 
                        {chatConversationColumns}
                    FROM "HootOut"."ChatConversation" m
                    WHERE m."Id" = @ChatConversationId
                    """,
                    new { ChatConversationId = conversationId }
                    , cancellationToken: ct));
        }
    }
}
