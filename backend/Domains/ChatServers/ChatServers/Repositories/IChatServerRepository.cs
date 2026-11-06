using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.Common;

namespace HootOut.ChatServers.Repositories
{
    public interface IChatServerRepository
    {
        Task<PageResult<ChatServerDto>> GetServerDtosByUserIdAsync(PageRequest request, Guid userId, CancellationToken ct = default);
        Task<ChatServerDto?> GetServerDtoByIdAsync(Guid serverId, CancellationToken ct);
    }
}
