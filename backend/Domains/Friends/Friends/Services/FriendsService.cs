using FluentValidation;
using FluentValidation.Results;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Enums;
using HootOut.Contracts.Common.Saver;
using HootOut.Contracts.Friends.Dtos;
using HootOut.Contracts.Friends.Requests;
using HootOut.Contracts.Friends.Services;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Services;
using HootOut.Friends.Entities;
using HootOut.Friends.Mappers;
using HootOut.Friends.Repositories;

namespace HootOut.Friends.Services
{
    public sealed class FriendsService : IFriendsService
    {
        private readonly ISaver<FriendRequest> friendRequestSaver;
        private readonly IUserService userService;
        private readonly IFriendRequestRepository friendRequestRepository;
        private readonly IFriendRelationRepository friendRelationRepository;

        public FriendsService(
            ISaver<FriendRequest> friendRequestSaver,
            IUserService userService,
            IFriendRequestRepository friendRequestRepository,
            IFriendRelationRepository friendRelationRepository
            )
        {
            this.friendRequestSaver = friendRequestSaver ?? throw new ArgumentNullException(nameof(friendRequestSaver));
            this.userService = userService ?? throw new ArgumentNullException(nameof(userService));
            this.friendRequestRepository = friendRequestRepository ?? throw new ArgumentNullException(nameof(friendRequestRepository));
            this.friendRelationRepository = friendRelationRepository ?? throw new ArgumentNullException(nameof(friendRelationRepository));
        }

        public async Task<FriendRequestDto> SendFriendRequestAsync(SendFriendRequest request, Guid userId, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.Validate();

            UserDto? invitedUser = await userService.GetUserDtoByUsernameAsync(request.Username, ct);

            if (invitedUser == null)
            {
                throw new ValidationException(new[] { new ValidationFailure("Username", "Cannot find a user with this username") });
            }

            if (invitedUser.Id == userId)
            {
                throw new ValidationException(new[] { new ValidationFailure("Username", "You cannot send a friend request to yourself") });
            }

            if (await friendRelationRepository.AreFriendsAsync(userId, invitedUser.Id, ct))
            {
                throw new ValidationException(new[] { new ValidationFailure("Username", "You are already friends with this user") });
            }

            var pending = await friendRequestRepository.GetPendingFriendRequestAsync(userId, invitedUser.Id, ct);
            if (pending != null)
            {
                if (pending.InvitedBy == userId)
                {
                    throw new ValidationException(new[] { new ValidationFailure("Username", "You already sent a friend request to this user") });
                }

                // The other user had already invited this one: accept that request instead of opening a new one
                var relationId = await friendRequestRepository.AcceptFriendRequestAsync(pending, ct);
                if (relationId == null)
                {
                    throw new ValidationException(new[] { new ValidationFailure("Invitation", "Invalid invitation") });
                }

                pending.Status = InvitationStatus.Accepted;
                return pending.ToDto();
            }

            var friendInvitation = Persistable.CreateNew<FriendRequest>();
            friendInvitation.UserId = invitedUser.Id;
            friendInvitation.InvitedBy = userId;
            friendInvitation.Status = InvitationStatus.Pending;

            await friendRequestSaver.SaveAsync(friendInvitation, ct);

            return friendInvitation.ToDto();
        }

        public async Task<Guid?> UpdateFriendRequestAsync(UpdateFriendRequest request, Guid userId, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.Validate();

            switch (request.Status)
            {
                case InvitationStatus.Accepted:
                    return await AcceptFriendRequestAsync(request, userId, ct);

                case InvitationStatus.Rejected:
                    return await RejectFriendRequestAsync(request, userId, ct);

                case InvitationStatus.Revoked:
                    return await RevokeFriendRequestAsync(request, userId, ct);
                default:
                    throw new ValidationException(new[] { new ValidationFailure("Status", "Invalid invitation status") });
            }
        }

        public async Task<PageResult<FriendRequestDto>> GetReceivedFriendRequestsAsync(PageRequest request, Guid userId, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(request);

            return await friendRequestRepository.GetReceivedFriendRequestsAsync(request, userId, ct);
        }

        public async Task<PageResult<FriendRequestDto>> GetSentFriendRequestsAsync(PageRequest request, Guid userId, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(request);

            return await friendRequestRepository.GetSentFriendRequestsAsync(request, userId, ct);
        }

        public async Task<PageResult<FriendRelationDto>> GetUserFriendsAsync(PageRequest request, Guid userId, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(request);

            return await friendRelationRepository.GetUserFriendsAsync(request, userId, ct);
        }

        public async Task ValidateUserCanSendMessageAsync(Guid chatConversationId, Guid userId, CancellationToken ct)
        {
            var directConversationId = await friendRelationRepository.GetDirectConverationByConversationAndUserId(chatConversationId, userId, ct);
            if (directConversationId == null)
            {
                throw new ValidationException(new[] { new ValidationFailure("Message", "The conversation could not be found") });
            }
        }

        private async Task<Guid?> AcceptFriendRequestAsync(UpdateFriendRequest request, Guid userId, CancellationToken ct)
        {
            var invitation = await ValidateFriendInvitation(request.Id, ct);

            if (invitation.UserId != userId)
            {
                throw new ValidationException(new[] { new ValidationFailure("Invitation", "You cannot accept this invitation") });
            }

            return await friendRequestRepository.AcceptFriendRequestAsync(invitation, ct);
        }

        private async Task<Guid?> RejectFriendRequestAsync(UpdateFriendRequest request, Guid userId, CancellationToken ct)
        {
            var invitation = await ValidateFriendInvitation(request.Id, ct);

            if (invitation.UserId != userId)
            {
                throw new ValidationException(new[] { new ValidationFailure("Invitation", "You cannot reject this invitation") });
            }

            invitation.Status = InvitationStatus.Rejected;
            await friendRequestSaver.SaveAsync(invitation, ct);

            return null;
        }
        private async Task<Guid?> RevokeFriendRequestAsync(UpdateFriendRequest request, Guid userId, CancellationToken ct)
        {
            var invitation = await ValidateFriendInvitation(request.Id, ct);

            if (invitation.InvitedBy != userId)
            {
                throw new ValidationException(new[] { new ValidationFailure("Invitation", "You cannot revoke this invitation") });
            }

            invitation.Status = InvitationStatus.Revoked;
            await friendRequestSaver.SaveAsync(invitation, ct);

            return null;
        }

        private async Task<FriendRequest> ValidateFriendInvitation(Guid invitationId, CancellationToken ct)
        {
            FriendRequest? invitation = await friendRequestRepository.GetFriendRequestByIdAsync(invitationId, ct);

            if (invitation == null || invitation.Status != InvitationStatus.Pending)
            {
                throw new ValidationException(new[] { new ValidationFailure("Invitation", "Invalid invitation") });
            }

            return invitation;
        }
    }
}
