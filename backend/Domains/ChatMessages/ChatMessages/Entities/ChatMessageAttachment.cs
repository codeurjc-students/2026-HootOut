using HootOut.Contracts.Common.Saver;

namespace HootOut.ChatMessages.Entities
{
    public sealed class ChatMessageAttachment : Persistable
    {
        public Guid MessageId { get; set; }
        public Guid MediaFileId { get; set; }
    }
}