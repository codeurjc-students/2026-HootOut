using HootOut.Contracts.WebSocket;

namespace HootOut.Contracts.WebSockets.Services
{
    public interface IWSPubSubService
    {
        Task HandleSubscribeAsync(Guid connectionId, string userId, string channel);

        Task HandleUnsubscribeAsync(Guid connectionId, string userId, string channel);

        Task SendMessageToChannel(Guid connectionId, string channel, WebSocketMessage message);

        Task RemoveAllConnectionSubscriptions(Guid connectionId);
    }
}
