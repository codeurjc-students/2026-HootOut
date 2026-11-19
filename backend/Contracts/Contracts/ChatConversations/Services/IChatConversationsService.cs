using HootOut.Contracts.ChatConversations.Dtos;

namespace HootOut.Contracts.ChatConversations.Services
{
    public interface IChatConversationsService
    {
        Task<ChatConversationDto?> GetChatConversationByIdAsync(Guid conversationId, CancellationToken ct);
    }
}
