using FluentValidation;
using FluentValidation.Results;
using HootOut.ChatServers.Entities;
using HootOut.ChatServers.Mappers;
using HootOut.ChatServers.Repositories;
using HootOut.CommonDomain.FileStorage;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.ChatServers.Requests;
using HootOut.Contracts.ChatServers.Services;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Enums;
using HootOut.Contracts.Common.Saver;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Services;

namespace HootOut.ChatServers.Services
{
    public sealed class ChatServerService : IChatServerService
    {
        private readonly ISaver<ChatServer> serverSaver;
        private readonly ISaver<ServerMember> memberSaver;
        private readonly ISaver<ServerInvitation> serverInvitationSaver;
        private readonly IChatServerRepository serverRepository;
        private readonly IServerMemberRepository memberRepository;
        private readonly IServerInvitationRepository invitationRepository;
        private readonly IUserService userService;
        private readonly IMediaUrlService mediaUrlService;

        public ChatServerService(
            ISaver<ChatServer> serverSaver,
            ISaver<ServerMember> memberSaver,
            ISaver<ServerInvitation> serverInvitationSaver,
            IChatServerRepository serverRepository,
            IUserService userService,
            IServerInvitationRepository invitationRepository,
            IServerMemberRepository memberRepository,
            IMediaUrlService mediaUrlService
            )
        {
            this.serverSaver = serverSaver ?? throw new ArgumentNullException(nameof(serverSaver));
            this.serverRepository = serverRepository ?? throw new ArgumentNullException(nameof(serverRepository));
            this.memberSaver = memberSaver ?? throw new ArgumentNullException(nameof(memberSaver));
            this.serverInvitationSaver = serverInvitationSaver ?? throw new ArgumentNullException(nameof(serverInvitationSaver));
            this.userService = userService ?? throw new ArgumentNullException(nameof(userService));
            this.invitationRepository = invitationRepository ?? throw new ArgumentNullException(nameof(invitationRepository));
            this.memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
            this.mediaUrlService = mediaUrlService ?? throw new ArgumentException(nameof(mediaUrlService));
        }

        public async Task<ChatServerDto> CreateChatServerAsync(CreateChatServerRequest request, Guid ownerId, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            request.Validate();

            var newServer = Persistable.CreateNew<ChatServer>();

            newServer.Name = request.Name;
            newServer.Description = request.Description;
            newServer.ProfilePictureId = request.ProfilePictureId;
            newServer.OwnerId = ownerId;

            await serverSaver.SaveAsync(newServer, ct);



            var member = Persistable.CreateNew<ServerMember>();
            member.ChatServerId = newServer.Id;
            member.UserId = ownerId;

            await memberSaver.SaveAsync(member, ct);

            var server = newServer.ToDto();

            if (server.ProfilePicture != null)
            {
                server.ProfilePicture.Url = await mediaUrlService.GetViewUrlAsync(server.ProfilePicture.MediaFileId, server.ProfilePicture.ContentType, server.ProfilePicture.FileName) ?? "";
            }

            return server;
        }

        public async Task<PageResult<ChatServerDto>> GetServerDtosByUserIdAsync(PageRequest request, Guid userId, CancellationToken ct = default)
        {
            var servers = await serverRepository.GetServerDtosByUserIdAsync(request, userId, ct);
            foreach(var server in servers.Items)
            {
                if (server.ProfilePicture != null)
                {
                    server.ProfilePicture.Url = await mediaUrlService.GetViewUrlAsync(server.ProfilePicture.MediaFileId, server.ProfilePicture.ContentType, server.ProfilePicture.FileName) ?? "";
                }
            }
            return servers;
        }

        public async Task<ChatServerDto?> GetServerDtoByIdAsync(Guid serverId, Guid userId, CancellationToken ct = default)
        {
            await ValidateUserIsInServer(serverId, userId, ct);
            var server = await serverRepository.GetServerDtoByIdAsync(serverId, ct);
            if (server != null && server.ProfilePicture != null)
            {
                server.ProfilePicture.Url = await mediaUrlService.GetViewUrlAsync(server.ProfilePicture.MediaFileId, server.ProfilePicture.ContentType, server.ProfilePicture.FileName) ?? "";
            }
            return server;
        }

        public async Task<PageResult<ServerMemberDto>> GetServerMemberDtosAsync(Guid serverId, Guid userId, PageRequest request, CancellationToken ct = default)
        {
            await ValidateUserIsInServer(serverId, userId, ct);

            return await memberRepository.GetServerMemberDtosAsync(serverId, request, ct);
        }

        public async Task<PageResult<ServerInvitationDto>> GetServerInvitationsAsync(Guid serverId, Guid userId, PageRequest request, CancellationToken ct = default)
        {
            await ValidateUserIsInServer(serverId, userId, ct);

            return await invitationRepository.GetServerInvitationDtosAsync(serverId, request, ct);
        }

        public async Task<ServerInvitationDto> InviteMemberAsync(InviteServerMemberRequest request, Guid userId, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.Validate();

            await ValidateUserIsInServer(request.ChatServerId, userId, ct);
            UserDto? invitedUser = await userService.GetUserDtoByUsernameAsync(request.Username, ct);

            if (invitedUser == null)
            {
                throw new ValidationException(new[] { new ValidationFailure("Username", "Cannot find a user with this username") });
            }

            await ValidateUserIsAlreadyAMember(request.ChatServerId, invitedUser.Id, ct);

            var pending = await invitationRepository.GetPendingServerInvitationAsync(request.ChatServerId, invitedUser.Id, ct);
            if (pending != null)
            {
                throw new ValidationException(new[] { new ValidationFailure("Username", "This user already has a pending invitation to the server") });
            }

            var serverInvitation = Persistable.CreateNew<ServerInvitation>();
            serverInvitation.ChatServerId = request.ChatServerId;
            serverInvitation.UserId = invitedUser.Id;
            serverInvitation.InvitedBy = userId;
            serverInvitation.Status = InvitationStatus.Pending;

            await serverInvitationSaver.SaveAsync(serverInvitation, ct);
            return serverInvitation.ToDto();
        }

        public async Task<Guid?> UpdateServerInvitationAsync(UpdateServerInvitationRequest request, Guid userId, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.Validate();

            switch (request.Status)
            {
                case InvitationStatus.Accepted:
                    return await AcceptInvitationAsync(request, userId, ct);

                case InvitationStatus.Rejected:
                    return await RejectServerInvitation(request, userId, ct);

                case InvitationStatus.Revoked:
                    return await RevokeServerInvitation(request, userId, ct);
                default:
                    throw new ValidationException(new[] { new ValidationFailure("Status", "Invalid invitation status") });
            }
        }
        public Task<IEnumerable<Guid>> GetServerIdsByUserIdAsync(Guid userId, CancellationToken ct = default)
        {
            return memberRepository.GetServerIdsByUserIdAsync(userId, ct);
        }

        /// <summary>Members of the server. Throws ValidationException if <paramref name="userId"/> is not a member.</summary>
        public async Task<IEnumerable<Guid>> GetServerMemberIdsAsync(Guid serverId, Guid userId, CancellationToken ct = default)
        {
            ServerMember? member = await memberRepository.GetServerMemberAsync(serverId, userId, ct);
            if (member == null)
                throw new ValidationException(new[] { new ValidationFailure("member", "User is not a member of the server") });

            return await memberRepository.GetServerMemberIdsAsync(serverId, ct);
        }

        public async Task<PageResult<ServerInvitationDto>> GetUserServerInvitations(Guid userId, PageRequest request, CancellationToken ct = default)
        {
            return await invitationRepository.GetUserServerInvitationDtosAsync(userId, request, ct);
        }

        public async Task ValidateUserCanSendMessageAsync(Guid conversationId, Guid userId, CancellationToken ct)
        {
            var serverId = await serverRepository.GetServerIdByConversationAndUserIdAsync(conversationId, userId, ct);
            if (serverId == null)
            {
                throw new ValidationException(new[] { new ValidationFailure("Message", $"The conversation could not be found {conversationId} {userId}", new { conversationId, userId }) });
            }
        }

        private async Task<Guid?> AcceptInvitationAsync(UpdateServerInvitationRequest request, Guid userId, CancellationToken ct)
        {
            var invitation = await ValidateServerInvitation(request.Id, userId, ct);

            return await invitationRepository.AcceptServerInvitationAsync(invitation, ct);
        }

        private async Task<Guid?> RejectServerInvitation(UpdateServerInvitationRequest request, Guid userId, CancellationToken ct)
        {
            var invitation = await ValidateServerInvitation(request.Id, userId, ct);

            invitation.Status = InvitationStatus.Rejected;
            await serverInvitationSaver.SaveAsync(invitation, ct);

            return null;
        }
        private async Task<Guid?> RevokeServerInvitation(UpdateServerInvitationRequest request, Guid userId, CancellationToken ct)
        {
            ServerInvitation? invitation = await invitationRepository.GetServerInvitationByIdAsync(request.Id, ct);

            if (invitation == null || invitation.Status != InvitationStatus.Pending)
            {
                throw new ValidationException(new[] { new ValidationFailure("Invitation", "Invalid invitation") });
            }

            ServerMember? member = await memberRepository.GetServerMemberAsync(invitation.ChatServerId, userId, ct);
            if (member == null)
                throw new ValidationException(new[] { new ValidationFailure("Member", "User is not a member of the server") });

            invitation.Status = InvitationStatus.Revoked;
            await serverInvitationSaver.SaveAsync(invitation, ct);

            return null;
        }

        private async Task<ServerInvitation> ValidateServerInvitation(Guid invitationId, Guid userId, CancellationToken ct)
        {
            ServerInvitation? invitation = await invitationRepository.GetServerInvitationByIdAsync(invitationId, ct);

            if (invitation == null || invitation.UserId != userId || invitation.Status != InvitationStatus.Pending)
            {
                throw new ValidationException(new[] { new ValidationFailure("Invitation", "Invalid invitation") });
            }

            await ValidateUserIsAlreadyAMember(invitation.ChatServerId, userId, ct);


            return invitation;
        }

        private async Task ValidateUserIsInServer(Guid serverId, Guid userId, CancellationToken ct)
        {
            ServerMember? member = await memberRepository.GetServerMemberAsync(serverId, userId, ct);
            if (member == null)
            {
                throw new ValidationException(new[] { new ValidationFailure("Validation", "The user is not a member of the server") });
            }
            return;
        }

        private async Task ValidateUserIsAlreadyAMember(Guid serverId, Guid userId, CancellationToken ct)
        {
            ServerMember? member = await memberRepository.GetServerMemberAsync(serverId, userId, ct);
            if (member != null)
                throw new ValidationException(new[] { new ValidationFailure("Member", "User is already a member") });
        }
    }
}
