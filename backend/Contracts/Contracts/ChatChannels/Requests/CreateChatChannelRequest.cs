using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.Contracts.ChatChannels.Requests
{
    public class CreateChatChannelRequest
    {
        public string? Name { get; set; }

        public Guid ServerId { get; set; }
    }
}
