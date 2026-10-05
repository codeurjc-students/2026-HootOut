using HootOut.Contracts.ChatChannels.Dtos;
using HootOut.Contracts.ChatChannels.Requests;

namespace HootOut.Contracts.ChatChannels.Services
{
    public interface IChatChannelsService
    {
        ChatChannelDto CreateChatChannel(CreateChatChannelRequest request);

        IEnumerable<ChatChannelDto> GetAllChatChannelsDtos();
    }
}
