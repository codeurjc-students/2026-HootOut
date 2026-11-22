using HootOut.Contracts.Common.Enums;
using HootOut.Contracts.Common.Saver;

namespace HootOut.ChatServers.Entities
{
    public sealed class ServerInvitation : Persistable
    {
        public Guid ChatServerId { get; set; }
        public Guid InvitedBy { get; set; }
        public Guid UserId { get; set; }
        public InvitationStatus Status { get; set; }
    }
}
