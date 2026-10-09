using HootOut.Contracts.WebSockets.Handlers;

namespace HootOut.Contracts.WebSockets.Dtos
{
    public class WebSocketMessageDto
    {
        public Guid? Id { get; set; }

        public Guid? ReplyTo { get; set; }
        public WSHandlerType Type { get; set; }

        public string? Channel { get; set; }

        public string? Payload { get; set; }

        public string? Error { get; set; }
    }
}
