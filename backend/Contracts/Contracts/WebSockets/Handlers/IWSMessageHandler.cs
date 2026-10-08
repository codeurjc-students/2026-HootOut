using HootOut.Contracts.WebSocket;
using HootOut.Contracts.WebSockets.Dtos;

namespace HootOut.Contracts.WebSockets.Handlers
{
    public interface IWSMessageHandler
    {
        Task HandleMessageAsync(Guid connectionId, WSUserInfo user, WebSocketMessage message);

        WSHandlerType Type { get; }
    }
}
