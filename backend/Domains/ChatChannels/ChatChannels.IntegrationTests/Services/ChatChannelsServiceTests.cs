using Autofac;
using FluentAssertions;
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

        public ChatChannelsServiceTests(PostgresTestContainer postgressContainer)
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

            chatChannelsService = container.Resolve<IChatChannelsService>();
        }
        public async ValueTask InitializeAsync()
        {

            await clearTables.ClearTablesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await clearTables.ClearTablesAsync();
        }

        [Fact]
        public async Task GetChatChannels_Empty()
        {
            var ct = TestContext.Current.CancellationToken;
            (await chatChannelsService.GetAllChatChannelsDtosAsync(ct)).Should().BeEmpty();
        }

        [Fact]
        public async Task GetChatChannels_One()
        {
            var ct = TestContext.Current.CancellationToken;
            string name = "nameTest";
            Guid serverId = Guid.NewGuid();
            await chatChannelsService.CreateChatChannelAsync(new CreateChatChannelRequest
            {
                Name = name,
                ServerId = serverId
            }, ct);

            IEnumerable<ChatChannelDto> result = await chatChannelsService.GetAllChatChannelsDtosAsync(ct);
            result.Should().HaveCount(1);
            ChatChannelDto chatChannel = result.Single();

            chatChannel.Id.Should().NotBeEmpty();
            //chatChannel.ChatServerId.Should().Be(serverId); //TO-DO is being set at the service
            chatChannel.Name.Should().Be(name);
            chatChannel.CreatedAt.Should().NotBe(default);
            chatChannel.ModifiedAt.Should().NotBe(default);
        }

        [Fact]
        public async Task GetChannels_Many()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid serverId = Guid.NewGuid();
            await chatChannelsService.CreateChatChannelAsync(new CreateChatChannelRequest { Name = "1", ServerId = serverId }, ct);
            await chatChannelsService.CreateChatChannelAsync(new CreateChatChannelRequest { Name = "2", ServerId = serverId }, ct);
            await chatChannelsService.CreateChatChannelAsync(new CreateChatChannelRequest { Name = "1", ServerId = serverId }, ct);
            await chatChannelsService.CreateChatChannelAsync(new CreateChatChannelRequest { Name = "1", ServerId = Guid.NewGuid() }, ct);

            (await chatChannelsService.GetAllChatChannelsDtosAsync(ct)).Should().HaveCount(4);
        }
    }
}
