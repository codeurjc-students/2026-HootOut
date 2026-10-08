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
                value.Init();
            }

            chatMessageService = container.Resolve<IChatMessageService>();
        }
        public ValueTask InitializeAsync()
        {

            clearTables?.ClearTables();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            clearTables?.ClearTables();
            return ValueTask.CompletedTask;
        }

        [Fact]
        public async Task GetChatMessages_Empty()
        {
            chatMessageService.GetMessagesByChannel(Guid.NewGuid()).Should().BeEmpty();
        }

        [Fact]
        public async Task GetChatMessages_One()
        {
            Guid channelId = Guid.NewGuid(); 
            string content = "This is a test message for a test case";

            var userService = container.Resolve<IUserService>();
            var user = userService.CreateUser(new CreateUserRequest
            {
                Email = "test@test.com",
                Username = "test123",
                Password = "123"
            });

            chatMessageService.CreateChatMessage(new CreateChatMessageRequest
            {
                ChatChannelId = channelId,
                AuthorId = user.Id,
                Content = content
            });

            IEnumerable<ChatMessageDto> result = chatMessageService.GetMessagesByChannel(channelId);
            result.Should().HaveCount(1);
            ChatMessageDto chatMessage = result.Single();

            chatMessage.Id.Should().NotBeEmpty();
            chatMessage.ChatChannelId.Should().Be(channelId);
            chatMessage.Author?.Id.Should().Be(user.Id);
            chatMessage.Author?.Username.Should().Be(user.Username);
            chatMessage.Content.Should().Be(content);
            chatMessage.CreatedAt.Should().NotBe(default); 

            result = chatMessageService.GetMessagesByChannel(Guid.NewGuid());
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetChannels_Many()
        {
            Guid channelId = Guid.NewGuid();
            Guid senderId = Guid.NewGuid();
            chatMessageService.CreateChatMessage(new CreateChatMessageRequest { ChatChannelId = channelId, AuthorId = senderId, Content = "test" });
            chatMessageService.CreateChatMessage(new CreateChatMessageRequest { ChatChannelId = channelId, AuthorId = senderId, Content = "test" });
            chatMessageService.CreateChatMessage(new CreateChatMessageRequest { ChatChannelId = channelId, AuthorId = Guid.NewGuid(), Content = "test" });
            chatMessageService.CreateChatMessage(new CreateChatMessageRequest { ChatChannelId = channelId, AuthorId = Guid.NewGuid(), Content = "test" });

            chatMessageService.CreateChatMessage(new CreateChatMessageRequest { ChatChannelId = Guid.NewGuid(), AuthorId = Guid.NewGuid(), Content = "test" });
            chatMessageService.CreateChatMessage(new CreateChatMessageRequest { ChatChannelId = Guid.NewGuid(), AuthorId = Guid.NewGuid(), Content = "test" });
            chatMessageService.CreateChatMessage(new CreateChatMessageRequest { ChatChannelId = Guid.NewGuid(), AuthorId = Guid.NewGuid(), Content = "test" });

            chatMessageService.GetMessagesByChannel(channelId).Should().HaveCount(4);
        }
    }
}
