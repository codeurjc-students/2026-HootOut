using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.Contracts.Authentication.Dtos
{
    public sealed class WsTicketIdentityDto
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = "";
    }
}
