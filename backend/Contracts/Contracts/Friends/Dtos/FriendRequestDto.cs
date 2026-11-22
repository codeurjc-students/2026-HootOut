using HootOut.Contracts.Common.Enums;

namespace HootOut.Contracts.Friends.Dtos
{
    public sealed record FriendRequestDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public Guid InvitedById { get; set; }
        public string InvitedByUsername { get; set; } = string.Empty;
        public InvitationStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
    }
}
