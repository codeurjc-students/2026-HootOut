namespace HootOut.Contracts.Common.Messages.Services
{
    public interface IQueuePubService
    {
        Task StartConsuming<T>(string exchange, Func<string, string, T, Task> onMessage, CancellationToken ct = default) where T : class;
        Task Publish<T>(string exchange, string rotuingKey, T @event, CancellationToken ct = default) where T : class;
        Task Subscribe(string exchange, string routingKey, CancellationToken ct = default);
        Task UnSubscribe(string exchange, string routingKey, CancellationToken ct = default);
    }
}
