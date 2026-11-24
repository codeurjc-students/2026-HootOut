using HootOut.Contracts.Common.Saver;

namespace HootOut.ChatServers.Entities
{
    public sealed class ChatServer : Persistable
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public Guid? ProfilePictureId { get; set; }

        public Guid OwnerId { get; set; }
    }
}
