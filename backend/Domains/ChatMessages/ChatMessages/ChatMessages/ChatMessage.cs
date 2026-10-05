using HootOut.Contracts.Common.Saver;

namespace HootOut.ChatMessages.ChatMessages
{
    public class ChatMessage : Persistable
    {
        public Guid AuthorId { get; set; }
        public Guid ChatChannelId { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}
