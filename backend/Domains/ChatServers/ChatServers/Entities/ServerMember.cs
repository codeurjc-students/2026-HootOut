using HootOut.Contracts.Common.Saver;

namespace HootOut.ChatServers.Entities
{
    public sealed class ServerMember : Persistable
    {
        public Guid ChatServerId { get; set; }
        public Guid UserId { get; set; }
        public Guid? InvitationId { get; set; }
    }
}
