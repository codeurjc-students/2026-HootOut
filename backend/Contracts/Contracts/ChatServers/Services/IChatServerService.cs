using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.ChatServers.Requests;
using HootOut.Contracts.Common;

namespace HootOut.Contracts.ChatServers.Services
{
    public interface IChatServerService
    {
        Task<ChatServerDto> CreateChatServerAsync(CreateChatServerRequest request, Guid ownerId, CancellationToken ct = default);
        Task<PageResult<ChatServerDto>> GetServerDtosByUserIdAsync(PageRequest request, Guid userId, CancellationToken ct = default);
        Task<ChatServerDto?> GetServerDtoByIdAsync(Guid serverId, Guid userId, CancellationToken ct = default);
        Task<PageResult<ServerInvitationDto>> GetUserServerInvitations(Guid userId, PageRequest request, CancellationToken ct = default);
        Task<PageResult<ServerInvitationDto>> GetServerInvitationsAsync(Guid serverId, Guid userId, PageRequest request, CancellationToken ct = default);
        Task<ServerInvitationDto> InviteMemberAsync(InviteServerMemberRequest request, Guid invitedBy, CancellationToken ct = default);
        Task<Guid?> UpdateServerInvitationAsync(UpdateServerInvitationRequest request, Guid userId, CancellationToken ct = default);
        Task<PageResult<ServerMemberDto>> GetServerMemberDtosAsync(Guid serverId, Guid userId, PageRequest request, CancellationToken ct = default);
    }
}
