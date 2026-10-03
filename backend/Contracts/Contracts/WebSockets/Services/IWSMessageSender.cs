using HootOut.Contracts.WebSocket;

namespace HootOut.Contracts.WebSockets.Services
{
    public interface IWSMessageSender
    {
        Task SendMessageAsync(Guid connectionId, WebSocketMessage message);
    }
}
