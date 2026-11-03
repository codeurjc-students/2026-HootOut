using HootOut.ChatServers.Entities;
using HootOut.Contracts.ChatServers.Dtos;

namespace HootOut.ChatServers.Repositories
{
    public interface IChatServerRepository
    {
        Task<IEnumerable<ChatServer>> GetServersByUserAsync(Guid userId, CancellationToken ct = default);
        Task<ChatServerDto?> GetServerDtoByIdAsync(Guid serverId, CancellationToken ct);
    }
}
