using HootOut.Contracts.ChatChannels.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.Contracts.ChatServers.Dtos
{
    public class ChatServerDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public IEnumerable<ChatChannelDto>? Channels { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime ModifiedAt { get; set; }
    }
}
