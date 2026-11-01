using HootOut.Contracts.ChatServers.Enums;
using HootOut.Contracts.Common.Saver;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.ChatServers.Entities
{
    public sealed class ServerInvitation: Persistable
    {
        public Guid ChatServerId { get; set; }
        public Guid InvitedBy { get; set; }
        public Guid UserId { get; set; }
        public ServerInvitationStatus Status { get; set; }
    }
}
