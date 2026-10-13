using HootOut.Contracts.ChatChannels.Dtos;
using HootOut.Contracts.ChatChannels.Requests;

namespace HootOut.Contracts.ChatChannels.Services
{
    public interface IChatChannelsService
    {
        Task<ChatChannelDto> CreateChatChannelAsync(CreateChatChannelRequest request, CancellationToken ct = default);

        Task<IEnumerable<ChatChannelDto>> GetAllChatChannelsDtosAsync(CancellationToken ct = default);
    }
}
