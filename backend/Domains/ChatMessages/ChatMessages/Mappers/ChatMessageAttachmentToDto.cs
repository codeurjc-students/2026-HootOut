using HootOut.ChatMessages.Entities;
using HootOut.Contracts.ChatMessage.Dtos;

namespace HootOut.ChatMessages.Mappers
{
    public static class ChatMessageAttachmentToDto
    {
        public static ChatMessageAttachmentDto ToDto(this ChatMessageAttachment attachment)
        {
            return new ChatMessageAttachmentDto
            {
                Id = attachment.Id,
                MessageId = attachment.MessageId,
                MediaFileId = attachment.MediaFileId
            };
        }
    }
}
