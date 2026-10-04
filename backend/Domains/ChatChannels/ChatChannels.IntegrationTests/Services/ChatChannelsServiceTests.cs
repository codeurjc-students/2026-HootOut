using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Contracts.ChatChannels.Dtos;
using HootOut.Contracts.ChatChannels.Requests;
using HootOut.Contracts.ChatChannels.Services; 
using HootOut.Users.IntegrationTests;  

namespace HootOut.ChatChannels.IntegrationTests.Services
{
    public class ChatChannelsServiceTests: IClassFixture<PostgresTestContainer>, IAsyncLifetime
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
                value.Init();
            }

            chatChannelsService = container.Resolve<IChatChannelsService>();
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
        public async Task GetChatChannels_Empty()
        {
            chatChannelsService.GetAllChatChannelsDtos().Should().BeEmpty();
        }

        [Fact]
        public async Task GetChatChannels_One()
        {
            string name = "nameTest";
            Guid serverId = Guid.NewGuid();
            chatChannelsService.CreateChatChannel(new CreateChatChannelRequest
            {
                Name = name,
                ServerId = serverId
            });

            IEnumerable<ChatChannelDto> result = chatChannelsService.GetAllChatChannelsDtos();
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
            Guid serverId = Guid.NewGuid();
            chatChannelsService.CreateChatChannel(new CreateChatChannelRequest { Name = "1", ServerId = serverId });
            chatChannelsService.CreateChatChannel(new CreateChatChannelRequest { Name = "2", ServerId = serverId });
            chatChannelsService.CreateChatChannel(new CreateChatChannelRequest { Name = "1", ServerId = serverId });
            chatChannelsService.CreateChatChannel(new CreateChatChannelRequest { Name = "1", ServerId = Guid.NewGuid() });

            chatChannelsService.GetAllChatChannelsDtos().Should().HaveCount(4);
        } 
    }
}
