using HootOut.CommonDomain.DefaultValues;
using HootOut.Contracts.Authentication.Requests;
using HootOut.Contracts.Authentication.Services;
using HootOut.Contracts.ChatChannels.Requests;
using HootOut.Contracts.ChatChannels.Services;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.ChatServers.Enums;
using HootOut.Contracts.ChatServers.Requests;
using HootOut.Contracts.ChatServers.Services;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Services;

namespace HootOut.HootOutAPI.Init
{
    public class DataInit : IDefaultValues
    {
        private readonly IUserService userService;
        private readonly IChatServerService serverService;
        private readonly IAuthenticationService authService;
        private readonly IChatChannelsService channelsService;
        private readonly IChatMessageService messageService;

        public DataInit(IUserService userService,
            IChatServerService serverService,
            IAuthenticationService authService,
            IChatChannelsService channelsService,
            IChatMessageService messageService)
        {
            this.userService = userService ?? throw new ArgumentNullException(nameof(userService));
            this.serverService = serverService ?? throw new ArgumentNullException(nameof(serverService));
            this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
            this.channelsService = channelsService ?? throw new ArgumentNullException(nameof(channelsService));
            this.messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        }

        public async Task InitAsync(CancellationToken ct = default)
        {
            string? initString = Environment.GetEnvironmentVariable("INIT_DATA");
            if (initString == null || !string.Equals("true", initString, StringComparison.InvariantCultureIgnoreCase))
                return;

            var mainName = "adminTest";
            var mainUser = await userService.GetUserDtoByUsernameAsync(mainName, ct);

            if (mainUser == null)
            {

                var req = new AuthRegistrationRequest
                {
                    Username = mainName,
                    Email = "adminTest@hootout.com",
                    Password = "Passwordtest123"
                };

                await authService.RegisterUserAsync(req, ct);
                mainUser = await userService.GetUserDtoByUsernameAsync(mainName, ct);
            }

            // Create Users
            var users = await CreateUsers(ct);
            // Create Servers
            var servers = await CreateServers(mainUser!, ct);

            //Invite all the users all the servers

            Dictionary<Guid, List<ServerInvitationDto>> userInvitations = new();
            foreach (var server in servers.Values)
            {
                foreach (var user in users.Values)
                {
                    var invitation = await serverService.InviteMemberAsync(new InviteServerMemberRequest
                    {
                        ChatServerId = server.Id,
                        Username = user.Username!
                    }, mainUser!.Id, ct);

                    if (!userInvitations.TryGetValue(user.Id, out var invitations))
                        invitations = new();
                    invitations.Add(invitation);
                    userInvitations[user.Id] = invitations;

                }
            }

            var halfTheUsers = users.Values.Take(users.Count / 2).ToList();
            //Make half of the users members of all the servers

            foreach (var user in halfTheUsers)
            {
                foreach (var invitation in userInvitations[user.Id])
                {
                    //var invitation = serverService.getUserInvitations()
                    await serverService.UpdateServerInvitationAsync(new UpdateServerInvitationRequest
                    {
                        Id = invitation.Id,
                        Status = ServerInvitationStatus.Accepted
                    }, user.Id, ct);
                }
            }

            // Send some messages on the servers (randomly)
            Random random = new();

            foreach (var user in halfTheUsers)
            {
                foreach (var server in servers.Values)
                {
                    foreach (var channel in server.Channels)
                    {
                        if (random.Next(0, 100) < 50) // Random 50%
                        {
                            await messageService.CreateChatMessageAsync(new CreateChatMessageRequest
                            {
                                AuthorId = user.Id,
                                ChatChannelId = channel.Id,
                                Content = Guid.NewGuid().ToString()
                            }, ct);
                        }
                    }
                }
            }
        }

        private async Task<Dictionary<Guid, UserDto>> CreateUsers(CancellationToken ct)
        {
            Dictionary<Guid, UserDto> users = new();
            for (int i = 0; i < 150; i++)
            {
                var username = $"Hootout-{Guid.CreateVersion7().ToString()}";
                var req = new AuthRegistrationRequest
                {
                    Username = username,
                    Email = $"{username}@a.a",
                    Password = Guid.NewGuid().ToString()
                };

                await authService.RegisterUserAsync(req, ct);
                var userDto = await userService.GetUserDtoByUsernameAsync(username, ct);
                users.Add(userDto!.Id, userDto);
            }

            return users;
        }

        private async Task<Dictionary<Guid, ChatServerDto>> CreateServers(UserDto mainUser, CancellationToken ct)
        {
            Dictionary<Guid, ChatServerDto> servers = new();
            for (int i = 0; i < 30; i++)
            {
                var server = await serverService.CreateChatServerAsync(new CreateChatServerRequest
                {
                    Name = Guid.NewGuid().ToString()
                }, mainUser.Id, ct);
                servers.Add(server.Id, server);

                for (int j = 0; j < 20; j++)
                {
                    var channel = await channelsService.CreateChatChannelAsync(new CreateChatChannelRequest
                    {
                        ChatServerId = server.Id,
                        Name = Guid.NewGuid().ToString()
                    }, ct);
                    server.Channels.Add(channel);
                }
            }

            return servers;
        }
    }
}
