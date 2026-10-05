using HootOut.ChatMessages.ChatMessages;
using HootOut.Contracts.ChatMessage.Dtos;

namespace HootOut.ChatMessages.Searchs
{
    public interface IChatMessageSearch
    {
        IEnumerable<ChatMessage> GetMessagesByChannelId(Guid channelId);
    }
}
