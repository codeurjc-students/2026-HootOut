using FluentValidation;
using FluentValidation.Results;
using HootOut.ChatServers.Entities;
using HootOut.ChatServers.Repositories;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.ChatServers.Services;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Presence;
using Microsoft.Extensions.Logging;

namespace HootOut.ChatServers.Services
{
    /// <summary>
    /// Online and offline members of a server.
    /// <list type="number">
    /// <item>The ids of the members come from Postgres (index ux_server_member_active).</item>
    /// <item>Which of them are online: one SMISMEMBER on the set <c>{presence}:online</c> of Valkey (per 1000 members).</item>
    /// <item>Online: the members among those ids, sorted by username and capped (normally few people are online).</item>
    /// <item>Offline: the usual paginated query of members, excluding the online ids.</item>
    /// </list>
    /// After the first load the client keeps the lists up to date with the presence events of the websocket.
    /// A separate service, and not part of ChatServerService, so the services that do not need Valkey
    /// (and their tests) do not depend on it.
    /// </summary>
    public sealed class ServerMemberPresenceService : IServerMemberPresenceService
    {
        /// <summary>Most online members returned. The client shows "and N more" with the rest of the total.</summary>
        public const int MaxOnlineMembers = 200;

        public const string DefaultOfflineSort = "username";

        private readonly ILogger<ServerMemberPresenceService> logger;
        private readonly IServerMemberRepository memberRepository;
        // Lazy (Autofac resolves Lazy<T> on its own): creating the store creates the Valkey connection, and if that
        // fails (e.g. REDIS_HOST missing) it must fail here, inside the try, and not when the controller is created,
        // which would break every endpoint of ChatServersController
        private readonly Lazy<IPresenceStore> presenceStore;

        public ServerMemberPresenceService(
            ILogger<ServerMemberPresenceService> logger,
            IServerMemberRepository memberRepository,
            Lazy<IPresenceStore> presenceStore)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
            this.presenceStore = presenceStore ?? throw new ArgumentNullException(nameof(presenceStore));
        }

        public async Task<OnlineServerMembersDto> GetOnlineMembersAsync(Guid serverId, Guid userId, CancellationToken ct = default)
        {
            await ValidateUserIsInServer(serverId, userId, ct);

            var onlineIds = await GetOnlineMemberIdsAsync(serverId, ct);
            if (onlineIds.Count == 0)
                return new OnlineServerMembersDto();

            var items = await memberRepository.GetServerMemberDtosByUserIdsAsync(serverId, onlineIds, MaxOnlineMembers, ct);
            return new OnlineServerMembersDto { Items = items, Total = onlineIds.Count };
        }

        public async Task<PageResult<ServerMemberDto>> GetOfflineMembersAsync(Guid serverId, Guid userId, PageRequest request, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            await ValidateUserIsInServer(serverId, userId, ct);

            var onlineIds = await GetOnlineMemberIdsAsync(serverId, ct);
            var sorted = string.IsNullOrWhiteSpace(request.SortBy)
                ? request with { SortBy = DefaultOfflineSort }
                : request;

            return await memberRepository.GetServerMemberDtosAsync(serverId, sorted, onlineIds, ct);
        }

        private async Task<IReadOnlyCollection<Guid>> GetOnlineMemberIdsAsync(Guid serverId, CancellationToken ct)
        {
            var memberIds = await memberRepository.GetServerMemberIdsAsync(serverId, ct);
            try
            {
                return await presenceStore.Value.GetOnlineUsersAsync(memberIds, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Valkey down: the lists still work, with everybody offline. The presence events of the
                // websocket move them to the online list when it comes back
                logger.LogWarning(ex, "Could not read the presence of the members of server {ServerId}", serverId);
                return [];
            }
        }

        private async Task ValidateUserIsInServer(Guid serverId, Guid userId, CancellationToken ct)
        {
            // Same message as ChatServerService: a server that does not exist looks the same
            ServerMember? member = await memberRepository.GetServerMemberAsync(serverId, userId, ct);
            if (member == null)
                throw new ValidationException(new[] { new ValidationFailure("Validation", "The user is not a member of the server") });
        }
    }
}
