using Autofac;
using FluentAssertions;
using HootOut.ChatChannels.Entities;
using HootOut.ChatServers.Entities;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.FileStorage;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Saver;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.Contracts.Users.Services;
using HootOut.Users.Entities;
using Moq;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace HootOut.ChatMessages.IntegrationTests.Services
{
    [Collection("IntegrationTests")]
    public class ChatMessageServiceTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private IContainer container;
        private ClearAllTables clearTables;

        private IChatMessageService chatMessageService;
        private ISaver<ChatServer> serverSaver;
        private ISaver<ChatChannel> channelSaver;
        private ISaver<UserInfo> userSaver;

        public ChatMessageServiceTests(PostgresTestContainer postgressContainer)
        {
            var mediaServiceMock = new Mock<IMediaUrlService>();

            mediaServiceMock.Setup(
                x => x.GetDownloadUrlAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((Guid id, string _, string _) => Task.FromResult(id.ToString() ?? null));
            mediaServiceMock.Setup(
                x => x.GetViewUrlAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((Guid id, string _, string _) => Task.FromResult(id.ToString() ?? null));

            var builder = postgressContainer.RegisterTestPersistenceProvider();
            builder.RegisterInstance(mediaServiceMock.Object).As<IMediaUrlService>().SingleInstance();
            container = builder.Build();

            var testPersistenceProvider = container.Resolve<IPersistenceProvider>();
            Assert.Same(postgressContainer.postgreSQLProvider, testPersistenceProvider);

            clearTables = container.Resolve<ClearAllTables>();
            var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority); ;

            foreach (var value in defaultValues)
            {
                value.InitAsync().Wait();
            }

            chatMessageService = container.Resolve<IChatMessageService>();
            serverSaver = container.Resolve<ISaver<ChatServer>>();
            channelSaver = container.Resolve<ISaver<ChatChannel>>();
            userSaver = container.Resolve<ISaver<UserInfo>>();
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
        public async Task GetChatMessages_Empty()
        {
            var ct = TestContext.Current.CancellationToken;
            (await chatMessageService.GetMessagesByChannelAsync(Guid.NewGuid(), ct: ct)).Items.Should().BeEmpty();
        }

        [Fact]
        public async Task GetChatMessages_One()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = Guid.NewGuid();
            Guid channelId = Guid.NewGuid();
            Guid serverId = Guid.NewGuid();

            await userSaver.SaveAsync(new UserInfo
            {
                Id = userId,
                Username = "test123",
                Email = "test@test.com",
                Password = "123451"
            },ct);

            await serverSaver.SaveAsync(new ChatServer
            {
                Id = serverId,
                Name = "test123",
                OwnerId = userId
            }, ct);

            await channelSaver.SaveAsync(new ChatChannel
            {
                Id = channelId,
                Name = "test",
                ChatServerId = serverId,
            }, ct);

            string content = "This is a test message for a test case"; 

            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest
            {
                ChatChannelId = channelId,
                AuthorId = userId,
                Content = content
            }, ct);

            PageResult<ChatMessageDto> result = await chatMessageService.GetMessagesByChannelAsync(channelId, ct: ct);
            result.Items.Should().HaveCount(1);
            ChatMessageDto chatMessage = result.Items.Single();

            chatMessage.Id.Should().NotBeEmpty();
            chatMessage.ChatChannelId.Should().Be(channelId);
            chatMessage.Author?.Id.Should().Be(userId);
            chatMessage.Author?.Username.Should().Be("test123");
            chatMessage.Content.Should().Be(content);
            chatMessage.CreatedAt.Should().NotBe(default);

            result = await chatMessageService.GetMessagesByChannelAsync(Guid.NewGuid(), ct: ct);
            result.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task GetChannels_Many()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid serverId = Guid.NewGuid();
            Guid serverId2 = Guid.NewGuid();
            Guid channelId = Guid.NewGuid();
            Guid channelId2 = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            Guid userId2 = Guid.NewGuid();

            await userSaver.SaveAsync(new UserInfo
            {
                Id = userId,
                Username = "test123",
                Email = "test@test.com",
                Password = "123451"
            }, ct);

            await userSaver.SaveAsync(new UserInfo
            {
                Id = userId2,
                Username = "test321",
                Email = "test2@test.com",
                Password = "123451"
            }, ct);

            await serverSaver.SaveAsync(new ChatServer
            {
                Id = serverId,
                Name = "test123",
                OwnerId = userId
            }, ct);

            await channelSaver.SaveAsync(new ChatChannel
            {
                Id = channelId,
                Name = "test",
                ChatServerId = serverId,
            }, ct);

            await serverSaver.SaveAsync(new ChatServer
            {
                Id = serverId2,
                Name = "test123",
                OwnerId = userId
            }, ct);

            await channelSaver.SaveAsync(new ChatChannel
            {
                Id = channelId2,
                Name = "test",
                ChatServerId = serverId2,
            }, ct);


            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatChannelId = channelId, AuthorId = userId, Content = "test" }, ct);
            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatChannelId = channelId, AuthorId = userId, Content = "test" }, ct);
            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatChannelId = channelId, AuthorId = userId2, Content = "test" }, ct);
            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatChannelId = channelId, AuthorId = userId2, Content = "test" }, ct);

            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatChannelId = channelId2, AuthorId = userId2, Content = "test" }, ct);
            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatChannelId = channelId2, AuthorId = userId2, Content = "test" }, ct);
            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatChannelId = channelId2, AuthorId = userId2, Content = "test" }, ct);

            (await chatMessageService.GetMessagesByChannelAsync(channelId, ct: ct)).Items.Should().HaveCount(4);
        }
    }
}
