using HootOut.ChatServers.Entities;
using HootOut.ChatServers.Mappers;
using HootOut.ChatServers.Repositories;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.ChatServers.Requests;
using HootOut.Contracts.ChatServers.Services;
using HootOut.Contracts.Common.Saver;
using FluentValidation;
using FluentValidation.Results;
using HootOut.Contracts.ChatServers.Enums;
using HootOut.Contracts.Users.Services;
using HootOut.Contracts.Users.Dtos;

namespace HootOut.ChatServers.Services
{
    public sealed class ChatServerService : IChatServerService
    {
        private readonly ISaver<ChatServer> serverSaver;
        private readonly ISaver<ServerMember> memberSaver; 
        private readonly ISaver<ServerInvitation> serverInvitationSaver;
        private readonly IChatServerRepository serverRepository;
        private readonly IUserService userService;

        public ChatServerService(
            ISaver<ChatServer> serverSaver, 
            ISaver<ServerMember> memberSaver, 
            ISaver<ServerInvitation> serverInvitationSaver,
            IChatServerRepository serverRepository,
            IUserService userService
            )
        {
            this.serverSaver = serverSaver ?? throw new ArgumentNullException(nameof(serverSaver));
            this.serverRepository = serverRepository ?? throw new ArgumentNullException(nameof(serverRepository));
            this.memberSaver = memberSaver ?? throw new ArgumentNullException(nameof(memberSaver));
            this.serverInvitationSaver = serverInvitationSaver ?? throw new ArgumentNullException(nameof(serverInvitationSaver));
            this.userService = userService ?? throw new ArgumentNullException(nameof(userService));
        }

        public async Task<ChatServerDto> CreateChatServerAsync(CreateChatServerRequest request, Guid ownerId, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            request.Validate();

            var newServer = Persistable.CreateNew<ChatServer>();

            newServer.Name = request.Name;
            newServer.Description = request.Description;
            newServer.OwnerId = ownerId;

            await serverSaver.SaveAsync(newServer, ct);

            var member = Persistable.CreateNew<ServerMember>();
            member.ChatServerId = newServer.Id;
            member.UserId = ownerId;
            member.JoinedAt = member.CreatedAt;

            await memberSaver.SaveAsync(member, ct);

            return newServer.ToDto();
        } 

        public async Task<IEnumerable<ChatServerDto>> GetServersByUserAsync(Guid userId, CancellationToken ct = default)
        {
            return (await serverRepository.GetServersByUserAsync(userId, ct))
                .Select(x => x.ToDto())
                .ToList();
        }

        public async Task<ServerInvitationDto> InviteMemberAsync(InviteServerMemberRequest request, Guid userId, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.Validate();

            await ValidateUserIsInServer(request.ServerId, userId, ct);
            UserDto? invitedUser = await userService.GetUserDtoByUsernameAsync(request.Username, ct);

            if (invitedUser == null)
            {
                throw new ValidationException(new[] { new ValidationFailure("username", "Cannot find a user with this username") });
            }

            await ValidateUserIsAlreadyAMember(request.ServerId, invitedUser.Id, ct);

            var serverInvitation = Persistable.CreateNew<ServerInvitation>();
            serverInvitation.ChatServerId = request.ServerId;
            serverInvitation.UserId = invitedUser.Id;
            serverInvitation.InvitedBy = userId;
            serverInvitation.Status = ServerInvitationStatus.Pending;

            await serverInvitationSaver.SaveAsync(serverInvitation, ct); 
            return serverInvitation.ToDto();
        }

        public async Task<Guid?> UpdateServerInvitationAsync(UpdateServerInvitationRequest request, Guid userId, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.Validate();

            switch (request.Status)
            {
                case ServerInvitationStatus.Accepted:
                    return await AcceptInvitationAsync(request, userId, ct); 

                case ServerInvitationStatus.Rejected:
                    return await RejectServerInvitation(request, userId, ct); 

                case ServerInvitationStatus.Revoked:
                    return await RevokeServerInvitation(request, userId, ct); 
                default:
                    throw new ValidationException(new[] { new ValidationFailure("status", "Invalid invitation status") });
            }  
        }

        private async Task<Guid?> AcceptInvitationAsync (UpdateServerInvitationRequest request, Guid userId, CancellationToken ct)
        {
            var invitation = await ValidateServerInvitation(request.Id, userId, ct);

            return await serverRepository.AcceptServerInvitationAsync(invitation, ct); 
        }

        private async Task<Guid?> RejectServerInvitation(UpdateServerInvitationRequest request, Guid userId, CancellationToken ct)
        {
            var invitation = await ValidateServerInvitation(request.Id, userId, ct);

            invitation.Status = ServerInvitationStatus.Rejected;
            await serverInvitationSaver.SaveAsync(invitation, ct);

            return invitation.Id;
        }
        private async Task<Guid?> RevokeServerInvitation(UpdateServerInvitationRequest request, Guid userId, CancellationToken ct)
        {
            ServerInvitation? invitation = await serverRepository.GetServerInvitationByIdAsync(request.Id, ct);

            if (invitation == null)
            {
                throw new ValidationException(new[] { new ValidationFailure("invitation", "Invalid invitation") });
            }

            ServerMember? member = await serverRepository.GetServerMemberAsync(invitation.ChatServerId, userId, ct);
            if (member == null)
                throw new ValidationException(new[] { new ValidationFailure("member", "User is not a member of the server") });  

            invitation.Status = ServerInvitationStatus.Revoked;
            await serverInvitationSaver.SaveAsync(invitation, ct);

            return invitation.Id;
        }

        private async Task<ServerInvitation> ValidateServerInvitation(Guid invitationId, Guid userId, CancellationToken ct)
        {
            ServerInvitation? invitation = await serverRepository.GetServerInvitationByIdAsync(invitationId, ct);

            if (invitation == null || invitation.UserId != userId || invitation.Status != ServerInvitationStatus.Pending)
            {
                throw new ValidationException(new[] { new ValidationFailure("invitation", "Invalid invitation") });
            }

            await ValidateUserIsAlreadyAMember(invitation.ChatServerId, userId, ct);
           

            return invitation;
        }

        private async Task ValidateUserIsInServer(Guid serverId, Guid userId, CancellationToken ct)
        {
            ServerMember? member = await serverRepository.GetServerMemberAsync(serverId, userId, ct);
            if (member == null)
            {
                throw new ValidationException(new[] { new ValidationFailure("validation", "The user is not a member of the server")});
            }
            return;
        }

        private async Task ValidateUserIsAlreadyAMember(Guid serverId, Guid userId, CancellationToken ct)
        {
            ServerMember? member = await serverRepository.GetServerMemberAsync(serverId, userId, ct);
            if (member != null)
                throw new ValidationException(new[] { new ValidationFailure("member", "User is already a member") });
        }
    }
}
