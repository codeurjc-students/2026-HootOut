using HootOut.Contracts.ChatMessage.Dtos;

namespace HootOut.ChatMessages.Repositories
{
    public interface IChatMessageAttachmentRepository
    {
        Task<IEnumerable<ChatMessageAttachmentDto>> GetChatMessageAttachmentDtos(Guid messageId, CancellationToken ct = default);
    }
}
