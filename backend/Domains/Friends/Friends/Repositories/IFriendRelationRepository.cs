using HootOut.Contracts.Common;
using HootOut.Contracts.Friends.Dtos;
using HootOut.Contracts.Friends.Responses;
using HootOut.Contracts.Users.Dtos;

namespace HootOut.Friends.Repositories
{
    public interface IFriendRelationRepository
    {
        Task<PageResult<UserFriendResponse>> GetUserFriendsAsync(PageRequest request, Guid userId, CancellationToken ct);
        Task<Guid?> GetDirectConverationByConversationAndUserId(Guid chatConversationId, Guid userId, CancellationToken ct); 
        Task<bool> AreFriendsAsync(Guid userA, Guid userB, CancellationToken ct); 
    }
}
