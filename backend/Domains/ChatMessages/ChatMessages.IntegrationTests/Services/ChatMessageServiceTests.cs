using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.Contracts.Users.Services;

namespace HootOut.ChatMessages.IntegrationTests.Services
{
    [Collection("IntegrationTests")]
    public class ChatMessageServiceTests : IClassFixture<PostgresTestContainer>, IAsyncLifetime
    {
        private IContainer container;
        private ClearAllTables clearTables;

        private IChatMessageService chatMessageService;

        public ChatMessageServiceTests(PostgresTestContainer postgressContainer)
        {
            var builder = postgressContainer.RegisterTestPersistenceProvider();
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
            (await chatMessageService.GetMessagesByChannelAsync(Guid.NewGuid(), ct)).Should().BeEmpty();
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

            IEnumerable<ChatMessageDto> result = await chatMessageService.GetMessagesByChannelAsync(channelId, ct);
            result.Should().HaveCount(1);
            ChatMessageDto chatMessage = result.Single();

            chatMessage.Id.Should().NotBeEmpty();
            chatMessage.ChatChannelId.Should().Be(channelId);
            chatMessage.Author?.Id.Should().Be(user.Id);
            chatMessage.Author?.Username.Should().Be(user.Username);
            chatMessage.Content.Should().Be(content);
            chatMessage.CreatedAt.Should().NotBe(default);

            result = await chatMessageService.GetMessagesByChannelAsync(Guid.NewGuid(), ct);
            result.Should().BeEmpty();
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

            (await chatMessageService.GetMessagesByChannelAsync(channelId, ct)).Should().HaveCount(4);
        }
    }
}
