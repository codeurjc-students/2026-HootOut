using HootOut.ChatChannels.ChatChannels;
using HootOut.Contracts.ChatChannels.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.ChatChannels.Search
{
    public interface IChatChannelSearch
    {
        public IEnumerable<ChatChannel> getAllChannels();
    }
}
