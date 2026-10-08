using HootOut.Contracts.WebSockets.Dtos;
using System.Net.WebSockets;

namespace HootOut.HootOutWebsockets.Contracts
{
    public interface IWSConnectionHandler
    {
        Task ReadMessagesAsync(WebSocket websocket, Guid connectionId, WSUserInfo user);
    }
}
