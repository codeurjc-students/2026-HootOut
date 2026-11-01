using HootOut.Contracts.Common.Saver;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.ChatServers.Entities
{
    public sealed class ServerMember: Persistable
    {
        public Guid ChatServerId { get; set; }
        public Guid UserId { get; set; }
        public Guid? InvitationId { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}
