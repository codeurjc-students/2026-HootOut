using HootOut.CommonDomain.DefaultValues;
using HootOut.Contracts.Authentication.Requests;
using HootOut.Contracts.Authentication.Services;
using HootOut.Contracts.ChatChannels.Requests;
using HootOut.Contracts.ChatChannels.Services;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.ChatServers.Requests;
using HootOut.Contracts.ChatServers.Services;
using HootOut.Contracts.Common.Enums;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Services;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HootOut.HootOutAPI.Init
{
    public class DataInit : IDefaultValues
    {
        private readonly IUserService userService;
        private readonly IChatServerService serverService;
        private readonly IAuthenticationService authService;
        private readonly IChatChannelsService channelsService;
        private readonly IChatMessageService messageService;
        private readonly ILogger<DataInit> logger;

        public DataInit(IUserService userService,
            IChatServerService serverService,
            IAuthenticationService authService,
            IChatChannelsService channelsService,
            IChatMessageService messageService,
            ILogger<DataInit> logger)
        {
            this.userService = userService ?? throw new ArgumentNullException(nameof(userService));
            this.serverService = serverService ?? throw new ArgumentNullException(nameof(serverService));
            this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
            this.channelsService = channelsService ?? throw new ArgumentNullException(nameof(channelsService));
            this.messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InitAsync(CancellationToken ct = default)
        {
            string? initString = Environment.GetEnvironmentVariable("INIT_DATA");
            if (initString == null || !string.Equals("true", initString, StringComparison.InvariantCultureIgnoreCase))
                return;

            // Create main user
            logger.LogInformation("Creating main user");

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

                var token = await authService.RegisterUserAsync(req, ct);
                mainUser = await userService.GetUserDtoByUsernameAsync(mainName, ct);
            }

            logger.LogInformation("Creating test user");
            var testUserName = "testUser";
            var testUser = await userService.GetUserDtoByUsernameAsync(testUserName, ct);
            if (testUser == null)
            {

                var req = new AuthRegistrationRequest
                {
                    Username = testUserName,
                    Email = "testUser@hootout.com",
                    Password = "Passwordtest123"
                };

                var token = await authService.RegisterUserAsync(req, ct);
                testUser = await userService.GetUserDtoByUsernameAsync(testUserName, ct);
            }

            // Create Users
            logger.LogInformation("Creating users");
            var users = await CreateUsers(ct);

            if (testUser != null)
            {
                users.Add(testUser!.Id, testUser);
            }

            // Create Servers
            logger.LogInformation("Creating servers");
            var servers = await CreateServers(mainUser!, ct);

            //Invite all the users all the servers
            logger.LogInformation("Invite users to servers");

            Random random = new();

            var serverMembers = new Dictionary<Guid, List<Guid>>();

            foreach (var server in servers.Values)
            {
                foreach (var user in users.Values)
                {
                    var invitation = await serverService.InviteMemberAsync(new InviteServerMemberRequest
                    {
                        ChatServerId = server.Id,
                        Username = user.Username!
                    }, mainUser!.Id, ct);

                    // The users join random servers

                    if (random.Next(0, 100) < 50) // Random 50%
                    {
                        await serverService.UpdateServerInvitationAsync(new UpdateServerInvitationRequest
                        {
                            Id = invitation.Id,
                            Status = InvitationStatus.Accepted
                        }, user.Id, ct);

                        if (!serverMembers.TryGetValue(server.Id, out var members))
                        {
                            members = new();
                            serverMembers[server.Id] = members;
                        }
                        members.Add(user.Id);
                    }
                }
            }


            // Send some messages on the servers (randomly)
            logger.LogInformation("Users send messages in random servers");
            foreach (var server in servers.Values)
            {
                foreach (var memberId in serverMembers[server.Id] ?? [])
                {
                    foreach (var channel in server.Channels)
                    {
                        if (random.Next(0, 100) < 50) // Random 50%
                        {
                            await messageService.CreateChatMessageAsync(new CreateChatMessageRequest
                            {
                                AuthorId = memberId,
                                ChatConversationId = channel.ChatConversationId,
                                Content = Guid.NewGuid().ToString()
                            }, ct);
                        }
                    }
                }
            }

            logger.LogInformation("Data init finished.");
        }

        private async Task<Dictionary<Guid, UserDto>> CreateUsers(CancellationToken ct)
        {
            JsonWebTokenHandler jtwHandler = new();

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

                var token = await authService.RegisterUserAsync(req, ct);
                var userDto = new UserDto
                {
                    Id = Guid.Parse(jtwHandler.ReadJsonWebToken(token.AccessToken!).GetClaim("sub").Value),
                    Username = username,
                };
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
