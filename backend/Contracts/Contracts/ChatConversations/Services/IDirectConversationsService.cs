using HootOut.Contracts.ChatConversations.Dtos;

namespace HootOut.Contracts.ChatConversations.Services
{
    public interface IDirectConversationsService
    {
        Task<DirectConversationDto?> GetDirectConversationByIdAsync(Guid directConversationId, Guid userId, CancellationToken ct);
    }
}
