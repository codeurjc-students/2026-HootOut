using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Requests;

namespace HootOut.Contracts.ChatMessage.Services
{
    public interface IChatMessageService
    {
        Task<ChatMessageDto> CreateChatMessageAsync(CreateChatMessageRequest request, CancellationToken ct = default);
        Task<IEnumerable<ChatMessageDto>> GetMessagesByChannelAsync(Guid channelId, CancellationToken ct = default);
    }
}
