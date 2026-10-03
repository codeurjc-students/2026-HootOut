namespace HootOut.Contracts.WebSockets.Services
{
    public interface IWSMessageDeserializer
    {
        Task HandleMessageAsync(Guid connectionId, string userId, string rawMessage);
    }
}
