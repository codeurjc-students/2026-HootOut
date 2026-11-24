using HootOut.ChatConversations.Repositories;
using HootOut.Contracts.ChatConversations.Dtos;
using HootOut.Contracts.ChatConversations.Services;

namespace HootOut.ChatConversations.Services
{
    public class DirectConversationsService : IDirectConversationsService
    {
        private readonly IDirectConversationsRepository directConversationsRepository;

        public DirectConversationsService(IDirectConversationsRepository directConversationsRepository)
        {
            this.directConversationsRepository = directConversationsRepository ?? throw new ArgumentNullException(nameof(directConversationsRepository));
        }

        public async Task<DirectConversationDto?> GetDirectConversationByIdAsync(Guid directConversationId, Guid userId, CancellationToken ct)
        {
            return await directConversationsRepository.GetDirectConversationByIdAsync(directConversationId, userId, ct);
        }
    }
}
