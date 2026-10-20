using Autofac;
using FluentAssertions;
using HootOut.ChatChannels.Search;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Contracts.ChatChannels.Dtos;
using HootOut.Contracts.ChatChannels.Requests;
using HootOut.Contracts.ChatChannels.Services;

namespace HootOut.ChatChannels.IntegrationTests.Services
{
    [Collection("IntegrationTests")]
    public class ChatChannelsServiceTests : IClassFixture<PostgresTestContainer>, IAsyncLifetime
    {
        private IContainer container;
        private ClearAllTables clearTables;

        private IChatChannelsService chatChannelsService;

        public ChatChannelsServiceTests(PostgresTestContainer postgresContainer)
        {
            var builder = postgresContainer.RegisterTestPersistenceProvider();
            container = builder.Build();

            var testPersistenceProvider = container.Resolve<IPersistenceProvider>();
            Assert.Same(postgresContainer.postgreSQLProvider, testPersistenceProvider);

            clearTables = container.Resolve<ClearAllTables>();
            var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority); ;

            foreach (var value in defaultValues)
            {
                value.InitAsync().Wait();
            }

            chatChannelsService = container.Resolve<IChatChannelsService>();
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
        public async Task GetChatChannelsByServerId_Empty()
        {
            var ct = TestContext.Current.CancellationToken;
            (await chatChannelsService.GetChannelsByServerIdAsync(Guid.NewGuid(), ct)).Should().BeEmpty();
        }

        [Fact]
        public async Task GetChatChannelsByServerId_One()
        {
            var ct = TestContext.Current.CancellationToken;
            string name = "nameTest";
            Guid serverId = Guid.NewGuid();
            await chatChannelsService.CreateChatChannelAsync(new CreateChatChannelRequest
            {
                Name = name,
                ChatServerId = serverId
            }, ct);

            IEnumerable<ChatChannelDto> result = await chatChannelsService.GetChannelsByServerIdAsync(serverId, ct);
            result.Should().HaveCount(1);
            ChatChannelDto chatChannel = result.Single();

            chatChannel.Id.Should().NotBeEmpty();
            chatChannel.ChatServerId.Should().Be(serverId);
            chatChannel.Name.Should().Be(name);
            chatChannel.CreatedAt.Should().NotBe(default);
            chatChannel.ModifiedAt.Should().NotBe(default);
        }

        [Fact]
        public async Task GetChatChannelsByServerId_Many()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid serverId = Guid.NewGuid();
            await chatChannelsService.CreateChatChannelAsync(new CreateChatChannelRequest { Name = "1", ChatServerId = serverId }, ct);
            await chatChannelsService.CreateChatChannelAsync(new CreateChatChannelRequest { Name = "2", ChatServerId = serverId }, ct);
            await chatChannelsService.CreateChatChannelAsync(new CreateChatChannelRequest { Name = "1", ChatServerId = serverId }, ct);
            await chatChannelsService.CreateChatChannelAsync(new CreateChatChannelRequest { Name = "1", ChatServerId = Guid.NewGuid() }, ct);

            (await chatChannelsService.GetChannelsByServerIdAsync(serverId, ct)).Should().HaveCount(3);
        }

        [Fact]
        public async Task UpdateChannel_Ok()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid serverId = Guid.NewGuid();
            var newChannel = await chatChannelsService.CreateChatChannelAsync(new CreateChatChannelRequest { Name = "test", ChatServerId = serverId }, ct);

            IChatChannelSearch search = container.Resolve<IChatChannelSearch>();
            var channel = await search.GetChannelByIdAsync(newChannel.Id, ct);

            channel.Should().NotBeNull();
            channel.Id.Should().Be(newChannel.Id);
            channel.ChatServerId.Should().Be(newChannel.ChatServerId);
            channel.Name.Should().Be(newChannel.Name);

            var newName = "New Name";
            var updatedChannel = await chatChannelsService.UpdateChatChannelAsync(new UpdateChatChannelRequest { Id = channel.Id, Name = newName }, ct);
            updatedChannel.Should().NotBeNull();
            updatedChannel.Name.Should().Be(newName);

            channel = await search.GetChannelByIdAsync(newChannel.Id, ct);
            channel.Should().NotBeNull();
            channel.Name.Should().Be(newName);
        }

        [Fact]
        public async Task DeleteChannel_Ok()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid serverId = Guid.NewGuid();
            var newChannel = await chatChannelsService.CreateChatChannelAsync(new CreateChatChannelRequest { Name = "test", ChatServerId = serverId }, ct);

            IChatChannelSearch search = container.Resolve<IChatChannelSearch>();
            var channel = await search.GetChannelByIdAsync(newChannel.Id, ct);

            var id = await chatChannelsService.DeleteChatChannelAsync(channel!.Id, ct);
            id.Should().Be(channel.Id);

            var deletedChannel = await search.GetChannelByIdAsync(id, ct);
            deletedChannel.Should().BeNull();

            var channels = await chatChannelsService.GetChannelsByServerIdAsync(serverId, ct);
            channels.Should().NotBeNull();
            channels.Should().BeEmpty();
        }
    }
}
