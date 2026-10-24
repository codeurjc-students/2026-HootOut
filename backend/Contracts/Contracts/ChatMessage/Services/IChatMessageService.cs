
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.Common;

namespace HootOut.Contracts.ChatMessage.Services
{
    public interface IChatMessageService
    {
        Task<ChatMessageDto> CreateChatMessageAsync(CreateChatMessageRequest request, CancellationToken ct = default);
        Task<PageResult<ChatMessageDto>> GetMessagesByChannelAsync (Guid channelId, Guid? next = null, int pageSize = 10, CancellationToken ct = default);
    }
}
