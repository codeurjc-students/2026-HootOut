using HootOut.Contracts.ChatConversations.Enums;
using HootOut.Contracts.Common.Saver;

namespace HootOut.ChatConversations.Entities
{
    public sealed class ChatConversation : Persistable
    {
        public ChatConversationType ConversationType { get; set; }
    }
}
