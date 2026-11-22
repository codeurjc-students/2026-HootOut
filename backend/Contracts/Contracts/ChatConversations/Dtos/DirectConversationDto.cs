using HootOut.Contracts.Users.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.Contracts.ChatConversations.Dtos
{
    public sealed record DirectConversationDto
    {
        public Guid Id { get; set; }
        public required UserDto User1 { get; set; }
        public required UserDto User2 { get; set; }
        public Guid ChatConversationId { get; set; }
    }
}
