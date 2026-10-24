using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.FileStorage;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Contracts.Common;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.Contracts.Users.Services;
using Moq;

namespace HootOut.ChatMessages.IntegrationTests.Services
{
    [Collection("IntegrationTests")]
    public class ChatMessageServiceTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private IContainer container;
        private ClearAllTables clearTables;

        private IChatMessageService chatMessageService;

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
            Guid channelId = Guid.NewGuid();
            string content = "This is a test message for a test case";

            var userService = container.Resolve<IUserService>();
            var user = await userService.CreateUserAsync(new CreateUserRequest
            {
                Email = "test@test.com",
                Username = "test123",
                Password = "123"
            }, ct);

            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest
            {
                ChatChannelId = channelId,
                AuthorId = user.Id,
                Content = content
            }, ct);

            PageResult<ChatMessageDto> result = await chatMessageService.GetMessagesByChannelAsync(channelId, ct: ct);
            result.Items.Should().HaveCount(1);
            ChatMessageDto chatMessage = result.Items.Single();

            chatMessage.Id.Should().NotBeEmpty();
            chatMessage.ChatChannelId.Should().Be(channelId);
            chatMessage.Author?.Id.Should().Be(user.Id);
            chatMessage.Author?.Username.Should().Be(user.Username);
            chatMessage.Content.Should().Be(content);
            chatMessage.CreatedAt.Should().NotBe(default);

            result = await chatMessageService.GetMessagesByChannelAsync(Guid.NewGuid(), ct:ct);
            result.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task GetChannels_Many()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid channelId = Guid.NewGuid();
            Guid senderId = Guid.NewGuid();
            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatChannelId = channelId, AuthorId = senderId, Content = "test" }, ct);
            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatChannelId = channelId, AuthorId = senderId, Content = "test" }, ct);
            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatChannelId = channelId, AuthorId = Guid.NewGuid(), Content = "test" }, ct);
            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatChannelId = channelId, AuthorId = Guid.NewGuid(), Content = "test" }, ct);

            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatChannelId = Guid.NewGuid(), AuthorId = Guid.NewGuid(), Content = "test" }, ct);
            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatChannelId = Guid.NewGuid(), AuthorId = Guid.NewGuid(), Content = "test" }, ct);
            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatChannelId = Guid.NewGuid(), AuthorId = Guid.NewGuid(), Content = "test" }, ct);

            (await chatMessageService.GetMessagesByChannelAsync(channelId, ct: ct)).Items.Should().HaveCount(4);
        }
    }
}
