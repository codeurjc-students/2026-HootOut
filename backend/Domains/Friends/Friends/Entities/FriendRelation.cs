using HootOut.Contracts.Common.Saver;

namespace HootOut.Friends.Entities
{
    public sealed class FriendRelation : Persistable
    {
        Guid User1Id { get; set; }
        Guid User2Id { get; set; }
        Guid FriendInvitation { get; set; }
    }
}
