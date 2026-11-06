using HootOut.ChatServers.Entities;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.Common;

namespace HootOut.ChatServers.Repositories
{
    public interface IServerInvitationRepository
    {
        Task<PageResult<ServerInvitationDto>> GetServerInvitationDtosAsync(Guid serverId, PageRequest request, CancellationToken ct = default);
        Task<ServerInvitation?> GetServerInvitationByIdAsync(Guid invitationId, CancellationToken ct = default);
        Task<Guid> AcceptServerInvitationAsync(ServerInvitation invitation, CancellationToken ct = default);
        Task<PageResult<ServerInvitationDto>> GetUserServerInvitationDtosAsync(Guid userId, PageRequest request, CancellationToken ct = default);
    }
}
