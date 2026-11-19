using HootOut.Contracts.Common;
using HootOut.Contracts.Friends.Dtos;
using HootOut.Friends.Entities;

namespace HootOut.Friends.Repositories
{
    public interface IFriendRequestRepository
    {
        Task<PageResult<FriendRequestDto>> GetReceivedFriendRequestsAsync(PageRequest request, Guid userId, CancellationToken ct);
        Task<PageResult<FriendRequestDto>> GetSentFriendRequestsAsync(PageRequest request, Guid userId, CancellationToken ct);
        Task<FriendRequest?> GetFriendInvitationByIdAsync(Guid invitationId, CancellationToken ct);
        Task<Guid?> AcceptFriendRequestAsync(FriendRequest friendRequest, CancellationToken ct = default);
    }
}