using HootOut.Contracts.Common.Saver;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.ChatMessages.ChatMessages
{
    public class ChatMessage : Persistable
    {
        public Guid SenderId { get; set; }
        public Guid ChatChannelId { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}
