using HootOut.Contracts.Users.Dtos;

namespace HootOut.Contracts.Friends.Responses
{
    public class UserFriendResponse
    {
        public Guid FriendRelationId { get; set; }
        public required UserDto Friend { get; set; }
        public Guid ChatConversationId { get; set; }
        public Guid DirectConversationId { get; set; }
    }
}
