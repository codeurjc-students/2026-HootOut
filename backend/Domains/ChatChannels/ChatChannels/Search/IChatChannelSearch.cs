using HootOut.ChatChannels.ChatChannels;

namespace HootOut.ChatChannels.Search
{
    public interface IChatChannelSearch
    {
        Task<IEnumerable<ChatChannel>> getAllChannelsAsync(CancellationToken ct = default);

        Task<IEnumerable<ChatChannel>> getChannelsByServerId(Guid serverId, CancellationToken ct = default);
    }
}
