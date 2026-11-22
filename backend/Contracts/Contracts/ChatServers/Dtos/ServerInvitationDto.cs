using HootOut.Contracts.Common.Enums;

namespace HootOut.Contracts.ChatServers.Dtos
{
    public sealed record ServerInvitationDto
    {
        public Guid Id { get; set; }
        public Guid ChatServerId { get; set; }
        public string ServerName { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public InvitationStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
    }
}
