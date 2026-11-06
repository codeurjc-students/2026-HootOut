using HootOut.ChatChannels.Entities;

namespace HootOut.ChatChannels.Repositories
{
    public interface IChatChannelRepository
    {
        Task<ChatChannel?> GetChannelByIdAsync(Guid channelId, CancellationToken ct = default);

        Task<IEnumerable<ChatChannel>> GetChannelsByServerId(Guid serverId, CancellationToken ct = default);
    }
}
