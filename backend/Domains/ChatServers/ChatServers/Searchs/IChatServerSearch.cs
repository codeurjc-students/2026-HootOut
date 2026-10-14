using HootOut.ChatServers.ChatServers;

namespace HootOut.ChatServers.Searchs
{
    public interface IChatServerSearch
    {
        Task<IEnumerable<ChatServer>> GetAllChatServersAsync(CancellationToken ct = default);
    }
}
