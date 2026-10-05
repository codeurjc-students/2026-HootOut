namespace HootOut.Contracts.ChatChannels.Dtos
{
    public class ChatChannelDto
    {
        public Guid Id { get; set; }

        public Guid ChatServerId { get; set; }

        public string Name { get; set; } = String.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime ModifiedAt { get; set; }
    }
}
