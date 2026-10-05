using HootOut.Contracts.Common.Saver;

namespace HootOut.ChatServers.ChatServers
{
    public class ChatServer : Persistable
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public Guid ProfilePicture { get; set; }

        public Guid OwnerId { get; set; }
    }
}
