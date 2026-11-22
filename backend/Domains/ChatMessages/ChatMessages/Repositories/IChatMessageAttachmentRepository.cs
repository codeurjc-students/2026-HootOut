using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.MediaFile.Dtos;

namespace HootOut.ChatMessages.Repositories
{
    public interface IChatMessageAttachmentRepository
    {
        Task<IEnumerable<ChatMessageAttachmentDto>> GetChatMessageAttachmentDtos(Guid messageId, CancellationToken ct = default);

        Task<MediaFileDto?> GetMediaFileByIdAsync(Guid mediaFileId,  CancellationToken ct = default);
    }
}
