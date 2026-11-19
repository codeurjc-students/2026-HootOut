using HootOut.Contracts.Friends.Dtos;
using HootOut.Friends.Entities;

namespace HootOut.Friends.Mappers
{
    public static class FriendInvitationToDto
    {
        public static FriendRequestDto ToDto(this FriendRequest invitation)
        {
            return new FriendRequestDto
            {
                Id = invitation.Id,
                UserId = invitation.UserId,
                InvitedById = invitation.InvitedBy,
                Status = invitation.Status,
                CreatedAt = invitation.CreatedAt,
                ModifiedAt = invitation.ModifiedAt,
            };
        }
    }
}
