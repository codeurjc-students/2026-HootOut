namespace HootOut.Contracts.ChatMessage.Requests
{
    public class CreateChatMessageRequest
    {
        public Guid ChatChannelId { get; set; }
        public Guid AuthorId { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}
