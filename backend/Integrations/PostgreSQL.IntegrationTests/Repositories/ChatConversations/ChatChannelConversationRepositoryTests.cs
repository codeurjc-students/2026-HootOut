using Autofac;
using Dapper;
using FluentAssertions;
using HootOut.ChatChannels.Entities;
using HootOut.ChatConversations.Repositories;
using HootOut.ChatServers.Entities;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.Contracts.ChatConversations.Enums;
using HootOut.Contracts.Common.Enums;
using HootOut.Contracts.Common.Saver;
using HootOut.Friends.Entities;
using HootOut.Friends.Repositories;
using HootOut.Infraestructure.DI;
using HootOut.Users.Entities;

namespace HootOut.PostgreSQL.IntegrationTests.Repositories.ChatConversations
{
    [Collection("IntegrationTests")]
    public class ChatConversationRepositoryTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private readonly IContainer container;

        private readonly ClearAllTables clearTables;

        private readonly IChatConversationsRepository repository;

        private readonly ISaver<ChatServer> serverSaver;

        private readonly ISaver<ChatChannel> channelSaver;

        private readonly ISaver<UserInfo> userSaver;

        private readonly ISaver<FriendRequest> requestSaver;

        private readonly IFriendRequestRepository requestRepository;

        private readonly IPersistenceProvider provider;

        public ChatConversationRepositoryTests(PostgresTestContainer postgresContainer)
        {
            var builder = new ContainerBuilder();
            new RegistrationManager().RegisterAllAssemblies(builder);
            builder.RegisterInstance(postgresContainer.postgreSQLProvider!).As<IPersistenceProvider>().SingleInstance();
            container = builder.Build();

            provider = container.Resolve<IPersistenceProvider>();
            Assert.Same(postgresContainer.postgreSQLProvider, provider);

            clearTables = container.Resolve<ClearAllTables>();
            var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority);

            foreach (var value in defaultValues)
            {
                value.InitAsync().Wait();
            }

            repository = container.Resolve<IChatConversationsRepository>();
            serverSaver = container.Resolve<ISaver<ChatServer>>();
            channelSaver = container.Resolve<ISaver<ChatChannel>>();
            userSaver = container.Resolve<ISaver<UserInfo>>();
            requestSaver = container.Resolve<ISaver<FriendRequest>>();
            requestRepository = container.Resolve<IFriendRequestRepository>();
        }

        public async ValueTask InitializeAsync()
        {
            await clearTables.ClearTablesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await clearTables.ClearTablesAsync();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task GetChatConversationById_ChannelConversation_ReturnsChannelType()
        {
            var ct = TestContext.Current.CancellationToken;
            var channel = await CreateChannelAsync(ct);

            var result = await repository.GetChatConversationByIdAsync(channel.ChatConversationId, ct);

            result.Should().NotBeNull();
            result.Id.Should().Be(channel.ChatConversationId);
            result.ConversationType.Should().Be(ChatConversationType.Channel);
        }

        [Fact]
        public async Task GetChatConversationById_DirectConversation_ReturnsDirectType()
        {
            var ct = TestContext.Current.CancellationToken;
            var conversationId = await CreateDirectConversationAsync(ct);

            var result = await repository.GetChatConversationByIdAsync(conversationId, ct);

            result.Should().NotBeNull();
            result.Id.Should().Be(conversationId);
            result.ConversationType.Should().Be(ChatConversationType.Direct);
        }

        [Fact]
        public async Task GetChatConversationById_SeveralConversations_ReturnsRequestedOne()
        {
            var ct = TestContext.Current.CancellationToken;
            var channel = await CreateChannelAsync(ct);
            var directId = await CreateDirectConversationAsync(ct);

            var channelResult = await repository.GetChatConversationByIdAsync(channel.ChatConversationId, ct);
            var directResult = await repository.GetChatConversationByIdAsync(directId, ct);

            channelResult!.Id.Should().Be(channel.ChatConversationId);
            directResult!.Id.Should().Be(directId);
        }

        [Fact]
        public async Task GetChatConversationById_DeletedChannel_StillReturnsConversation()
        {
            var ct = TestContext.Current.CancellationToken;
            var channel = await CreateChannelAsync(ct);
            await channelSaver.DeleteAsync(channel, ct);

            var result = await repository.GetChatConversationByIdAsync(channel.ChatConversationId, ct);

            // Conversations have no Deleted column; access checks are done with the channel or the friendship
            result.Should().NotBeNull();
            result.ConversationType.Should().Be(ChatConversationType.Channel);
        }

        [Fact]
        public async Task GetChatConversationById_ChannelIdInsteadOfConversationId_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var channel = await CreateChannelAsync(ct);

            var result = await repository.GetChatConversationByIdAsync(channel.Id, ct);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetChatConversationById_NotExisting_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            await CreateChannelAsync(ct);

            var result = await repository.GetChatConversationByIdAsync(Guid.NewGuid(), ct);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetChatConversationById_CancelledToken_ThrowsOperationCanceled()
        {
            var channel = await CreateChannelAsync(TestContext.Current.CancellationToken);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var act = () => repository.GetChatConversationByIdAsync(channel.ChatConversationId, cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        private async Task<UserInfo> CreateUserAsync(string username, CancellationToken ct)
        {
            var user = Persistable.CreateNew<UserInfo>();
            user.Username = username;
            user.Email = $"{username}@test.com";
            user.Password = $"hash-{username}";
            await userSaver.SaveAsync(user, ct);
            return user;
        }

        /// <summary>
        /// The channel saver creates the channel conversation.
        /// </summary>
        private async Task<ChatChannel> CreateChannelAsync(CancellationToken ct)
        {
            var server = Persistable.CreateNew<ChatServer>();
            server.Name = $"server-{server.Id}";
            server.Description = "Test server";
            server.OwnerId = Guid.NewGuid();
            await serverSaver.SaveAsync(server, ct);

            var channel = Persistable.CreateNew<ChatChannel>();
            channel.ChatServerId = server.Id;
            channel.Name = "general";
            await channelSaver.SaveAsync(channel, ct);
            return channel;
        }

        /// <summary>
        /// Accepting a friend request creates the direct conversation, as the backend does.
        /// </summary>
        private async Task<Guid> CreateDirectConversationAsync(CancellationToken ct)
        {
            var sender = await CreateUserAsync("sender", ct);
            var receiver = await CreateUserAsync("receiver", ct);
            var request = Persistable.CreateNew<FriendRequest>();
            request.InvitedBy = sender.Id;
            request.UserId = receiver.Id;
            request.Status = InvitationStatus.Pending;
            await requestSaver.SaveAsync(request, ct);
            (await requestRepository.AcceptFriendRequestAsync(request, ct)).Should().NotBeNull();

            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(
                """
                    SELECT "ChatConversationId"
                    FROM "HootOut"."DirectConversation"
                    WHERE "User1Id" = LEAST(@A, @B) AND "User2Id" = GREATEST(@A, @B)
                """,
                new { A = sender.Id, B = receiver.Id },
                cancellationToken: ct));
        }
    }
}
