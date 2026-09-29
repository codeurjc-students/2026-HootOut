namespace HootOut.Contracts.WebSockets.Services
{
    public interface IWSMessageDeserializer
    {
        Task HandleMessageAsync(string connectionId, string userId, string rawMessage);
    }
}
