using HootOut.Contracts.WebSockets.Entities;

namespace HootOut.Contracts.WebSockets.Services
{
    public interface IWSPubSubService
    {
        Task HandleSubscribeAsync(Guid connectionId, string exchange, string channel, CancellationToken ct = default);

        Task HandleUnsubscribeAsync(Guid connectionId, string exchange, string channel, CancellationToken ct = default);

        Task SendMessageAsync(string exchange, string channel, WebSocketMessage message, CancellationToken ct);

        Task ReceiveMessageAsync(string exchange, string channel, WebSocketMessage message);

        Task RemoveAllConnectionSubscriptions(Guid connectionId, CancellationToken ct = default);
    }
}
