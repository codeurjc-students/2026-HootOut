using HootOut.Contracts.WebSocket;
using System.Net.WebSockets;

namespace HootOut.HootOutWebsockets.Services
{
    public interface IWSConnectionManager
    {
        Task<string> AddConnectionAsync(WebSocket websocket, HttpContext httpContext);

        Task RemoveConnectionAsync(string connectionId);

        Task<WebSocket?> GetWebSocketByConnectionIdAsync(string connectionId);

        Task SendMessageAsync(string connectionId, WebSocketMessage message);

        Task SendMessageAsync(WebSocket webSocket, string connectionId, WebSocketMessage message);
    }
}
