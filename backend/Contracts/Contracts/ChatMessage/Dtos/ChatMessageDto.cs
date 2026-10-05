using HootOut.Contracts.Users.Dtos;

namespace HootOut.Contracts.ChatMessage.Dtos
{
    public class ChatMessageDto
    {
        public Guid Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public UserDto? Author { get; set; }
        public string? Content { get; set; }
        public Guid ChatChannelId { get; set; }
    }
}
