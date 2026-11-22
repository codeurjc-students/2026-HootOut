using HootOut.Contracts.Common.Saver;

namespace HootOut.Friends.Entities
{
    public sealed class FriendRelation : Persistable
    {
        public Guid User1Id { get; set; }
        public Guid User2Id { get; set; }
        public Guid FriendRequestId { get; set; }
    }
}
