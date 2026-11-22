using HootOut.Contracts.ChatConversations.Enums;

namespace HootOut.Contracts.ChatConversations.Dtos
{
    public sealed record class ChatConversationDto
    {
        public Guid Id { get; set; }
        public ChatConversationType ConversationType { get; set; }
    }
}
