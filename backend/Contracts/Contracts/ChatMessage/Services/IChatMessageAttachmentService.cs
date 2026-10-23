using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Requests;

namespace HootOut.Contracts.ChatMessage.Services
{
    public interface IChatMessageAttachmentService
    {
        Task<IEnumerable<ChatMessageAttachmentDto>> CreateMessageAttachments(Guid messageId, IEnumerable<CreateChatAttachmentRequest> request, CancellationToken ct = default);
        Task<IEnumerable<ChatMessageAttachmentDto>> GetMessageAttachmentDtos(Guid messageId, CancellationToken ct = default);
        Task SetAttachmentsUrl(IEnumerable<ChatMessageAttachmentDto> attachment, CancellationToken ct);
    }
}
