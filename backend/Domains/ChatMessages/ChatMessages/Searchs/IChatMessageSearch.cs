using HootOut.ChatMessages.ChatMessages;

namespace HootOut.ChatMessages.Searchs
{
    public interface IChatMessageSearch
    {
        IEnumerable<ChatMessage> GetMessagesByChannelId(Guid channelId);
    }
}
