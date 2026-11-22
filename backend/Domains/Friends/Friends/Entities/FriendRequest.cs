using HootOut.Contracts.Common.Enums;
using HootOut.Contracts.Common.Saver;

namespace HootOut.Friends.Entities
{
    public sealed class FriendRequest : Persistable
    {
        public Guid InvitedBy { get; set; }
        public Guid UserId { get; set; }
        public InvitationStatus Status { get; set; }
    }
}
