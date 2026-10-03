using HootOut.Contracts.Common.Saver;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.ChatChannels.ChatChannels
{
    public class ChatChannel : Persistable
    {
        public Guid ChatServerId { get; set; }
        public string Name { get; set; } = string.Empty; 
    }
}
