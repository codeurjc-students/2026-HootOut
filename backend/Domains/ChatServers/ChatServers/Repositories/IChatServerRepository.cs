using HootOut.ChatServers.Entities;

namespace HootOut.ChatServers.Repositories
{
    public interface IChatServerRepository
    { 
        Task<IEnumerable<ChatServer>> GetServersByUserAsync(Guid userId, CancellationToken ct = default);

        Task<ServerMember?> GetServerMemberAsync(Guid serverId, Guid userId, CancellationToken ct = default);

        Task<ServerInvitation?> GetServerInvitationByIdAsync(Guid invitationId, CancellationToken ct = default);

        Task<Guid> AcceptServerInvitationAsync(ServerInvitation invitation, CancellationToken ct = default);
    }
}
