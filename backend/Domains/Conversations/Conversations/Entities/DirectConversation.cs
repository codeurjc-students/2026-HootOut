using HootOut.Contracts.Common.Saver;

namespace HootOut.ChatConversations.Entities
{
    public sealed class DirectConversation : Persistable
    {
        public Guid User1Id { get; set; }
        public Guid User2Id { get; set; }
        public Guid ConversationId { get; set; }
    }
}
