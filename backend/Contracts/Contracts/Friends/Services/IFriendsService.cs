using HootOut.Contracts.Common;
using HootOut.Contracts.Friends.Dtos;
using HootOut.Contracts.Friends.Requests;
using HootOut.Contracts.Friends.Responses;
using HootOut.Contracts.Users.Dtos;

namespace HootOut.Contracts.Friends.Services
{
    public interface IFriendsService
    {
        Task<FriendRequestDto> SendFriendRequestAsync(SendFriendRequest request, Guid userId, CancellationToken ct);
        Task<Guid?> UpdateFriendRequestAsync(UpdateFriendRequest request, Guid userId, CancellationToken ct);
        Task<PageResult<FriendRequestDto>> GetReceivedFriendRequestsAsync(PageRequest request, Guid userId, CancellationToken ct);
        Task<PageResult<FriendRequestDto>> GetSentFriendRequestsAsync(PageRequest request, Guid userId, CancellationToken ct);
        Task<PageResult<UserFriendResponse>> GetUserFriendsAsync(PageRequest request, Guid userId, CancellationToken ct); 
        Task ValidateUserCanSendMessageAsync(Guid chatConversationId, Guid userId, CancellationToken ct);
    }
}
