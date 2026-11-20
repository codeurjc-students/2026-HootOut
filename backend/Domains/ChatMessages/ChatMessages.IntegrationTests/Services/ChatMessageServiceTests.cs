using Autofac;
using FluentAssertions;
using HootOut.ChatChannels.Entities;
using HootOut.ChatServers.Entities;
using HootOut.ChatServers.Services;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.FileStorage;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Contracts.ChatServers.Requests;
using HootOut.Contracts.ChatServers.Services;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Saver;
using HootOut.Users.Entities;
using Moq;
using System.Threading.Channels;

namespace HootOut.ChatMessages.IntegrationTests.Services
{
    [Collection("IntegrationTests")]
    public class ChatMessageServiceTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private IContainer container;
        private ClearAllTables clearTables; 
        private IChatMessageService chatMessageService;
        private IChatServerService chatServerService;
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
            chatServerService = container.Resolve<IChatServerService>();
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
            (await chatMessageService.GetMessagesByConversationIdAsync(Guid.NewGuid(), ct: ct)).Items.Should().BeEmpty();
        }

        [Fact]
        public async Task GetChatMessages_One()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = Guid.NewGuid();
            Guid channelId = Guid.NewGuid(); 

            await userSaver.SaveAsync(new UserInfo
            {
                Id = userId,
                Username = "test123",
                Email = "test@test.com",
                Password = "123451"
            }, ct);

            var server = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest
            {
                Name = "test123"
            }, userId, ct); 

            var channel = new ChatChannel
            {
                Id = channelId,
                Name = "test",
                ChatServerId = server.Id,
            };

            await channelSaver.SaveAsync(channel, ct);

            string content = "This is a test message for a test case";

            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest
            {
                ChatConversationId = channel.ChatConversationid,
                AuthorId = userId,
                Content = content
            }, ct);

            PageResult<ChatMessageDto> result = await chatMessageService.GetMessagesByConversationIdAsync(channel.ChatConversationid, ct: ct);
            result.Items.Should().HaveCount(1);
            ChatMessageDto chatMessage = result.Items.Single();

            chatMessage.Id.Should().NotBeEmpty();
            chatMessage.ChatConversationId.Should().Be(channel.ChatConversationid);
            chatMessage.Author?.Id.Should().Be(userId);
            chatMessage.Author?.Username.Should().Be("test123");
            chatMessage.Content.Should().Be(content);
            chatMessage.CreatedAt.Should().NotBe(default);

            result = await chatMessageService.GetMessagesByConversationIdAsync(Guid.NewGuid(), ct: ct);
            result.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task GetMessages_Many()
        {
            var ct = TestContext.Current.CancellationToken; 
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

            var username2 = "test321";
            await userSaver.SaveAsync(new UserInfo
            {
                Id = userId2,
                Username = "test321",
                Email = "test2@test.com",
                Password = "123451"
            }, ct); 

            var server = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest
            {
                Name = "test123"
            }, userId, ct);

            var invitation = await chatServerService.InviteMemberAsync(new InviteServerMemberRequest
            {
                ChatServerId = server.Id,
                Username = username2
            }, userId, ct);

            await chatServerService.UpdateServerInvitationAsync(new UpdateServerInvitationRequest
            {
                Id = invitation.Id,
                Status = Contracts.Common.Enums.InvitationStatus.Accepted
            }, userId2, ct);


            var channel1 = new ChatChannel
            {
                Id = channelId,
                Name = "test",
                ChatServerId = server.Id,
            };

            await channelSaver.SaveAsync(channel1, ct); 

            var server2 = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest
            {
                Name = "test123"
            }, userId2, ct);

            var channel2 = new ChatChannel
            {
                Id = channelId2,
                Name = "test",
                ChatServerId = server2.Id,
            };
            await channelSaver.SaveAsync(channel2, ct);

            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatConversationId = channel1.ChatConversationid, AuthorId = userId, Content = "test" }, ct);
            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatConversationId = channel1.ChatConversationid, AuthorId = userId, Content = "test" }, ct);
            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatConversationId = channel1.ChatConversationid, AuthorId = userId2, Content = "test" }, ct);
            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatConversationId = channel1.ChatConversationid, AuthorId = userId2, Content = "test" }, ct);

            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatConversationId = channel2.ChatConversationid, AuthorId = userId2, Content = "test" }, ct);
            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatConversationId = channel2.ChatConversationid, AuthorId = userId2, Content = "test" }, ct);
            await chatMessageService.CreateChatMessageAsync(new CreateChatMessageRequest { ChatConversationId = channel2.ChatConversationid, AuthorId = userId2, Content = "test" }, ct);

            (await chatMessageService.GetMessagesByConversationIdAsync(channel1.ChatConversationid, ct: ct)).Items.Should().HaveCount(4);
        }
    }
}
