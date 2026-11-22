using HootOut.Contracts.ChatConversations.Dtos;

namespace HootOut.ChatConversations.Repositories
{
    public interface IChatConversationsRepository
    {
        Task<ChatConversationDto?> GetChatConversationByIdAsync(Guid conversationId, CancellationToken ct);
    }
}
