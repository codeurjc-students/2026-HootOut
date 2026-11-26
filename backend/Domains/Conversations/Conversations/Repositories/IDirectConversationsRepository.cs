using HootOut.Contracts.ChatConversations.Dtos;

namespace HootOut.ChatConversations.Repositories
{
    public interface IDirectConversationsRepository
    {
        Task<DirectConversationDto?> GetDirectConversationByIdAsync(Guid directConversatioId, Guid userId, CancellationToken ct);
    }
}
