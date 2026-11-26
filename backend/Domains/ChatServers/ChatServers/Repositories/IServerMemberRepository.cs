using HootOut.ChatServers.Entities;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.Common;

namespace HootOut.ChatServers.Repositories
{
    public interface IServerMemberRepository
    {
        Task<ServerMember?> GetServerMemberAsync(Guid serverId, Guid userId, CancellationToken ct = default);
        Task<PageResult<ServerMemberDto>> GetServerMemberDtosAsync(Guid serverId, PageRequest request, CancellationToken ct = default);
        Task<IEnumerable<Guid>> GetServerIdsByUserIdAsync(Guid userId, CancellationToken ct);
        Task<IEnumerable<Guid>> GetServerMemberIdsAsync(Guid serverId, CancellationToken ct);

        /// <summary>
        /// Same as <see cref="GetServerMemberDtosAsync(Guid, PageRequest, CancellationToken)"/> without the members whose
        /// UserId is in <paramref name="excludedUserIds"/> (items and total). Used for the offline members.
        /// </summary>
        Task<PageResult<ServerMemberDto>> GetServerMemberDtosAsync(Guid serverId, PageRequest request, IReadOnlyCollection<Guid> excludedUserIds, CancellationToken ct = default);

        /// <summary>
        /// Active members of the server among <paramref name="userIds"/>, sorted by username (same collation as the
        /// paginated list), at most <paramref name="limit"/>. Used for the online members.
        /// </summary>
        Task<IReadOnlyList<ServerMemberDto>> GetServerMemberDtosByUserIdsAsync(Guid serverId, IReadOnlyCollection<Guid> userIds, int limit, CancellationToken ct = default);
    }
}
