using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.Contracts.WebSockets.Dtos
{
    public sealed record WSUserInfo
    {
        public Guid UserId { get; init; }
        public string Username { get; init; } = string.Empty;
    }
}
