using HootOut.Contracts.WebSockets.Dtos;

namespace HootOut.Contracts.WebSockets.Services
{
    public interface IWSMessageSender
    {
        Task SendMessageAsync(Guid connectionId, byte[] rawMessage);

        Task SendMessageAsync(Guid connectionId, WebSocketMessageDto message);
    }
}
