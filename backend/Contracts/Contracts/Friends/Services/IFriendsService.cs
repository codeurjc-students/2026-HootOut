using HootOut.Contracts.Common;
using HootOut.Contracts.Friends.Dtos;
using HootOut.Contracts.Friends.Requests;

namespace HootOut.Contracts.Friends.Services
{
    public interface IFriendsService
    {
        Task<FriendRequestDto> SendFriendRequestAsync(SendFriendRequest request, Guid userId, CancellationToken ct);
        Task<Guid?> UpdateFriendRequestAsync(UpdateFriendRequest request, Guid userId, CancellationToken ct);
        Task<PageResult<FriendRequestDto>> GetReceivedFriendRequestsAsync(PageRequest request, Guid userId, CancellationToken ct);
        Task<PageResult<FriendRequestDto>> GetSentFriendRequests(PageRequest request, Guid userId, CancellationToken ct);
        Task<PageResult<FriendRelationDto>> GetUserFriends(PageRequest request, Guid userId, CancellationToken ct);
        Task ValidateUserCanSendMessageAsync(Guid chatConversationId, Guid userId, CancellationToken ct);
    }
}
