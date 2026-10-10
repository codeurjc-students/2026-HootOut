
using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Services;
using System.Net.WebSockets;

namespace HootOut.HootOutWebsockets.Contracts
{
    public interface IWSConnectionManager : IWSMessageSender
    {
        Task<Guid> AddConnectionAsync(WebSocket websocket, WSUserInfo user);

        Task RemoveConnectionAsync(Guid connectionId);

        Task<WSSession?> GetWebSocketSessionByConnectionIdAsync(Guid connectionId);

        Task SendMessageAsync(WSSession wsSession, Guid connectionId, byte[] rawMessage);
    }
}
