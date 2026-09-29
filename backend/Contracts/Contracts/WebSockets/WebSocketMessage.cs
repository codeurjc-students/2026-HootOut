using HootOut.Contracts.WebSockets.Handlers;

namespace HootOut.Contracts.WebSocket
{
    public class WebSocketMessage
    {
        public WSHandlerType Type { get; set; }

        public string? Channel { get; set; }

        public string? Payload { get; set; }
    }
}
