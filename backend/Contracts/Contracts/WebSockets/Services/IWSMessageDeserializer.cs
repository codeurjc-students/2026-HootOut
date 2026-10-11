using HootOut.Contracts.WebSockets.Dtos;

namespace HootOut.Contracts.WebSockets.Services
{
    public interface IWSMessageDeserializer
    {
        Task HandleMessageAsync(Guid connectionId, WSUserInfo user, string rawMessage, CancellationToken ct = default);
    }
}
