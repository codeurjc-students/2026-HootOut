using HootOut.ChatConversations.Repositories;
using HootOut.Contracts.ChatConversations.Dtos;
using HootOut.Contracts.ChatConversations.Services;

namespace HootOut.ChatConversations.Services
{
    public sealed class ChatConversationsService : IChatConversationsService
    {
        private readonly IChatConversationsRepository chatConversationsRepository;

        public ChatConversationsService(IChatConversationsRepository chatConversationsRepository)
        {
            this.chatConversationsRepository = chatConversationsRepository ?? throw new ArgumentNullException(nameof(chatConversationsRepository));
        }

        public async Task<ChatConversationDto?> GetChatConversationByIdAsync(Guid conversationId, CancellationToken ct)
        {
            return await chatConversationsRepository.GetChatConversationByIdAsync(conversationId, ct);
        }
    }
}
