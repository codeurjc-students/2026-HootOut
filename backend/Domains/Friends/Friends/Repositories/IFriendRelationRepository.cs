using HootOut.Contracts.Common;
using HootOut.Contracts.Friends.Dtos;

namespace HootOut.Friends.Repositories
{
    public interface IFriendRelationRepository
    {
        Task<PageResult<FriendRelationDto>> GetUserFriendsAsync(PageRequest request, Guid userId, CancellationToken ct);
        Task<Guid?> GetDirectConverationByConversationAndUserId(Guid chatConversationId, Guid userId, CancellationToken ct);
    }
}
