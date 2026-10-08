using HootOut.Contracts.ChatMessage.Dtos;

namespace HootOut.ChatMessages.Searchs
{
    public interface IChatMessageSearch
    {
        IEnumerable<ChatMessageDto> GetMessagesByChannelId(Guid channelId);
    }
}
