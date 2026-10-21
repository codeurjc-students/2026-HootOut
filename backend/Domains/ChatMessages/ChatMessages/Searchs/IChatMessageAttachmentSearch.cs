using HootOut.Contracts.ChatMessage.Dtos;

namespace HootOut.ChatMessages.Searchs
{
    public interface IChatMessageAttachmentSearch
    {
        Task<IEnumerable<ChatMessageAttachmentDto>> GetChatMessageAttachmentDtos(Guid messageId, CancellationToken ct = default);
    }
}
