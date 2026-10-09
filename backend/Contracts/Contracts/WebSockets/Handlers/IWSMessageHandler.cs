using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Entities;

namespace HootOut.Contracts.WebSockets.Handlers
{
    public interface IWSMessageHandler
    {
        Task HandleMessageAsync(Guid connectionId, WSUserInfo user, WebSocketMessage message, CancellationToken ct = default);

        WSHandlerType Type { get; }
    }
}
