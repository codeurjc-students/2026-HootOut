using HootOut.Contracts.ChatChannels.Dtos;
using HootOut.Contracts.ChatChannels.Requests;

namespace HootOut.Contracts.ChatChannels.Services
{
    public interface IChatChannelsService
    {
        Task<ChatChannelDto> CreateChatChannelAsync(CreateChatChannelRequest request, CancellationToken ct = default); 
        Task<ChatChannelDto> UpdateChatChannelAsync(UpdateChatChannelRequest request, CancellationToken ct = default);
        Task<Guid> DeleteChatChannelAsync(Guid channelId, CancellationToken ct = default);
        Task<IEnumerable<ChatChannelDto>> GetChannelsByServerIdAsync(Guid serverId, CancellationToken ct = default);
    }
}
