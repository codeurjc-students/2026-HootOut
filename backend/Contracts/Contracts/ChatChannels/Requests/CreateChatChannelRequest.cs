namespace HootOut.Contracts.ChatChannels.Requests
{
    public class CreateChatChannelRequest
    {
        public string? Name { get; set; }

        public Guid ServerId { get; set; }
    }
}
