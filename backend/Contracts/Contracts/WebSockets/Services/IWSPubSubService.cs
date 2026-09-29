using HootOut.Contracts.WebSocket;

namespace HootOut.Contracts.WebSockets.Services
{
    public interface IWSPubSubService
    {
        Task HandleSubscribeAsync(string connectionId, string userId, string channel);

        Task HandleUnsubscribeAsync(string connectionId, string userId, string channel);

        Task SendMessageToChannel(string channel, WebSocketMessage message);
    }
}
