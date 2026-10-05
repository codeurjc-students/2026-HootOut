using HootOut.ChatChannels.ChatChannels;

namespace HootOut.ChatChannels.Search
{
    public interface IChatChannelSearch
    {
        public IEnumerable<ChatChannel> getAllChannels();
    }
}
