using HootOut.Contracts.WebSocket;
using HootOut.Contracts.WebSockets.Services;
using System.Net.WebSockets;

namespace HootOut.HootOutWebsockets.Services
{
    public interface IWSConnectionManager : IWSMessageSender
    {
        Task<Guid> AddConnectionAsync(WebSocket websocket, HttpContext httpContext);

        Task RemoveConnectionAsync(Guid connectionId);

        Task<WebSocket?> GetWebSocketByConnectionIdAsync(Guid connectionId);

        Task SendMessageAsync(WebSocket webSocket, Guid connectionId, WebSocketMessage message);
    }
}
