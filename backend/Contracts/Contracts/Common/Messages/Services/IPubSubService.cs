using HootOut.Contracts.Common.Messages.Events;

namespace HootOut.Contracts.Common.Messages.Services
{
    public interface IPubSubService
    {
        Task Publish<T>(string queue, T @event) where T : MessageEvent;
        Task Subscribe<T>(string queue, Action<T> onMessagReceived) where T : MessageEvent;
    }
}
