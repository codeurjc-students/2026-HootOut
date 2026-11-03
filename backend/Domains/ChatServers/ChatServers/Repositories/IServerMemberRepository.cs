using HootOut.ChatServers.Entities;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.Common;

namespace HootOut.ChatServers.Repositories
{
    public interface IServerMemberRepository
    {
        Task<ServerMember?> GetServerMemberAsync(Guid serverId, Guid userId, CancellationToken ct = default);
        Task<PageResult<ServerMemberDto>> GetServerMemberDtosAsync(Guid serverId, PageRequest request, CancellationToken ct = default);
    }
}
