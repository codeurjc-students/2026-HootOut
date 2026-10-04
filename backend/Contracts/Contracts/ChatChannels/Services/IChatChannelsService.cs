using HootOut.Contracts.ChatChannels.Dtos;
using HootOut.Contracts.ChatChannels.Requests;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.Contracts.ChatChannels.Services
{
    public interface IChatChannelsService
    {
        ChatChannelDto CreateChatChannel(CreateChatChannelRequest request);

        IEnumerable<ChatChannelDto> GetAllChatChannelsDtos();
    }
}
