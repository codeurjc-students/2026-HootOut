using HootOut.Contracts.WebSocket;

namespace HootOut.Contracts.WebSockets.Handlers
{
    public interface IWSMessageHandler
    {
        Task HandleMessageAsync(string connectionId, string userId, WebSocketMessage message);

        WSHandlerType Type { get; }
    }
}
