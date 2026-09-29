using HootOut.Contracts.WebSocket;
using System.Net.WebSockets;

namespace HootOut.HootOutWebsockets.Services
{
    public interface IWSConnectionManager
    {
        Task AddConnectionAsync(WebSocket websocket, HttpContext httpContext);

        Task SendMessageAsync(string connectionId, WebSocketMessage message);
    }
}
