using HootOut.Contracts.Users.Dtos;

namespace HootOut.Contracts.Friends.Dtos
{
    public sealed record FriendRelationDto
    {
        public Guid Id { get; set; }
        public required UserDto User1Id { get; set; }
        public required UserDto User2Id { get; set; }
        public Guid DirectConversationId { get; set; } 
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
    }
}
