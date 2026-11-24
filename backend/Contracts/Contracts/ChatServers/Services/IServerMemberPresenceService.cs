using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.Common;

namespace HootOut.Contracts.ChatServers.Services
{
    /// <summary>
    /// Members of a server split by presence: who is online comes from Valkey, the rest from Postgres.
    /// Both methods throw ValidationException if <c>userId</c> is not a member of the server.
    /// </summary>
    public interface IServerMemberPresenceService
    {
        /// <summary>Online members sorted by username, capped. If Valkey cannot be reached nobody is online.</summary>
        Task<OnlineServerMembersDto> GetOnlineMembersAsync(Guid serverId, Guid userId, CancellationToken ct = default);

        /// <summary>
        /// Members that are not online, paginated with the same cursor as the list of members.
        /// Sorted by username (ascending unless the request says otherwise) when the request has no SortBy.
        /// </summary>
        Task<PageResult<ServerMemberDto>> GetOfflineMembersAsync(Guid serverId, Guid userId, PageRequest request, CancellationToken ct = default);
    }
}
