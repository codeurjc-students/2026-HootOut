using HootOut.ChatChannels.ChatChannels;

namespace HootOut.ChatChannels.Search
{
    public interface IChatChannelSearch
    {
        Task<ChatChannel?> GetChannelByIdAsync(Guid channelId, CancellationToken ct = default);

        Task<IEnumerable<ChatChannel>> GetChannelsByServerId(Guid serverId, CancellationToken ct = default);
    }
}
