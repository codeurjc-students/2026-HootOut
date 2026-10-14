using HootOut.Contracts.ChatMessage.Dtos;

namespace HootOut.ChatMessages.Searchs
{
    public interface IChatMessageSearch
    {
        Task<IEnumerable<ChatMessageDto>> GetMessagesByChannelIdAsync(Guid channelId, CancellationToken ct = default);
    }
}
