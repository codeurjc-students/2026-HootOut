using HootOut.Contracts.Common.Saver;

namespace HootOut.ChatChannels.ChatChannels
{
    public class ChatChannel : Persistable
    {
        public Guid ChatServerId { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
