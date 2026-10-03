using HootOut.Contracts.WebSockets.Handlers;

namespace HootOut.Contracts.WebSocket
{
    public class WebSocketMessage
    {
        public Guid? Id { get; set; }

        public Guid? ReplyTo { get; set; }
        public WSHandlerType Type { get; set; }

        public string? Channel { get; set; }

        public string? Payload { get; set; }

        public string? Error { get; set; }
    }
}
