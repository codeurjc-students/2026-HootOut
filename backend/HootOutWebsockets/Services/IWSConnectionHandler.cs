using System.Net.WebSockets;

namespace HootOut.HootOutWebsockets.Services
{
    public interface IWSConnectionHandler
    {
        Task ReadMessagesAsync(WebSocket websocket, Guid connectionId, string userId);
    }
}
