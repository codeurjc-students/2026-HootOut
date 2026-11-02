using HootOut.Contracts.Users.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.Contracts.ChatServers.Dtos
{
    public class ServerMemberDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public Guid UserId { get; set; } 
        public DateTime CreatedAt { get; set; }
    }
}