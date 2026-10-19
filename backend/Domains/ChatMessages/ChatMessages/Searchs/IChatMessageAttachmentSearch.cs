using HootOut.Contracts.ChatMessage.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.ChatMessages.Searchs
{
    public interface IChatMessageAttachmentSearch
    {
        Task<IEnumerable<ChatMessageAttachmentDto>> GetChatMessageAttachmentDtos(Guid messageId, CancellationToken ct = default);
    }
}
