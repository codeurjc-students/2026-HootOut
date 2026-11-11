using Autofac;
using FluentAssertions;
using HootOut.ChatChannels.Entities;
using HootOut.ChatChannels.Repositories;
using HootOut.ChatServers.Entities;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.Contracts.Common.Saver;
using HootOut.Infraestructure.DI;

namespace HootOut.PostgreSQL.IntegrationTests.Repositories.ChatChannels
{
    [Collection("IntegrationTests")]
    public class ChatChannelRepositoryTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private readonly IContainer container;

        private readonly ClearAllTables clearTables;

        private readonly IChatChannelRepository repository;

        private readonly ISaver<ChatChannel> channelSaver;

        private readonly ISaver<ChatServer> serverSaver;

        public ChatChannelRepositoryTests(PostgresTestContainer postgresContainer)
        {
            var builder = new ContainerBuilder();
            new RegistrationManager().RegisterAllAssemblies(builder);
            builder.RegisterInstance(postgresContainer.postgreSQLProvider!).As<IPersistenceProvider>().SingleInstance();
            container = builder.Build();

            var testPersistenceProvider = container.Resolve<IPersistenceProvider>();
            Assert.Same(postgresContainer.postgreSQLProvider, testPersistenceProvider);

            clearTables = container.Resolve<ClearAllTables>();
            var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority);

            foreach (var value in defaultValues)
            {
                value.InitAsync().Wait();
            }

            repository = container.Resolve<IChatChannelRepository>();
            channelSaver = container.Resolve<ISaver<ChatChannel>>();
            serverSaver = container.Resolve<ISaver<ChatServer>>();
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

        #region GetChannelsByServerId

        [Fact]
        public async Task GetChannelsByServerId_NoChannels_ReturnsEmpty()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);

            var result = await repository.GetChannelsByServerId(server.Id, ct);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetChannelsByServerId_NotExistingServer_ReturnsEmpty()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            await CreateChannelAsync(server.Id, "general", ct);

            var result = await repository.GetChannelsByServerId(Guid.NewGuid(), ct);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetChannelsByServerId_ReturnsMappedChannels()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            var expected = await CreateChannelAsync(server.Id, "general", ct);

            var result = (await repository.GetChannelsByServerId(server.Id, ct)).ToList();

            result.Should().ContainSingle();
            AssertMapped(result[0], expected);
        }

        [Fact]
        public async Task GetChannelsByServerId_SeveralServers_ReturnsOnlyChannelsOfRequestedServer()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            var otherServer = await CreateServerAsync(ct);
            var first = await CreateChannelAsync(server.Id, "general", ct);
            var second = await CreateChannelAsync(server.Id, "random", ct);
            await CreateChannelAsync(otherServer.Id, "general", ct);

            var result = await repository.GetChannelsByServerId(server.Id, ct);

            result.Select(c => c.Id).Should().BeEquivalentTo([first.Id, second.Id]);
            result.Should().OnlyContain(c => c.ChatServerId == server.Id);
        }

        [Fact]
        public async Task GetChannelsByServerId_ReturnsChannelsOrderedByName()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            await CreateChannelAsync(server.Id, "charlie", ct);
            await CreateChannelAsync(server.Id, "alpha", ct);
            await CreateChannelAsync(server.Id, "bravo", ct);

            var result = await repository.GetChannelsByServerId(server.Id, ct);

            result.Select(c => c.Name).Should().Equal("alpha", "bravo", "charlie");
        }

        [Fact]
        public async Task GetChannelsByServerId_DuplicatedNames_ReturnsAll()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            var first = await CreateChannelAsync(server.Id, "general", ct);
            var second = await CreateChannelAsync(server.Id, "general", ct);

            var result = await repository.GetChannelsByServerId(server.Id, ct);

            result.Select(c => c.Id).Should().BeEquivalentTo([first.Id, second.Id]);
        }

        [Fact]
        public async Task GetChannelsByServerId_DeletedChannel_IsExcluded()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            var kept = await CreateChannelAsync(server.Id, "general", ct);
            var deleted = await CreateChannelAsync(server.Id, "random", ct);
            await channelSaver.DeleteAsync(deleted, ct);

            var result = await repository.GetChannelsByServerId(server.Id, ct);

            result.Select(c => c.Id).Should().Equal(kept.Id);
        }

        #endregion

        #region GetChannelByIdAsync

        [Fact]
        public async Task GetChannelById_Existing_ReturnsMappedChannel()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            var expected = await CreateChannelAsync(server.Id, "general", ct);
            await CreateChannelAsync(server.Id, "random", ct);

            var result = await repository.GetChannelByIdAsync(expected.Id, ct);

            result.Should().NotBeNull();
            AssertMapped(result, expected);
        }

        [Fact]
        public async Task GetChannelById_NotExisting_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            await CreateChannelAsync(server.Id, "general", ct);

            var result = await repository.GetChannelByIdAsync(Guid.NewGuid(), ct);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetChannelById_Deleted_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            var channel = await CreateChannelAsync(server.Id, "general", ct);
            await channelSaver.DeleteAsync(channel, ct);

            var result = await repository.GetChannelByIdAsync(channel.Id, ct);

            result.Should().BeNull();
        }

        #endregion

        #region Cancellation

        [Theory]
        [InlineData(nameof(IChatChannelRepository.GetChannelsByServerId))]
        [InlineData(nameof(IChatChannelRepository.GetChannelByIdAsync))]
        public async Task AnyMethod_CancelledToken_ThrowsOperationCanceled(string method)
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync(ct);
            var channel = await CreateChannelAsync(server.Id, "general", ct);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            Func<Task> act = method switch
            {
                nameof(IChatChannelRepository.GetChannelsByServerId) => () => repository.GetChannelsByServerId(server.Id, cts.Token),
                nameof(IChatChannelRepository.GetChannelByIdAsync) => () => repository.GetChannelByIdAsync(channel.Id, cts.Token),
                _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
            };

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        #endregion

        private async Task<ChatServer> CreateServerAsync(CancellationToken ct)
        {
            var server = Persistable.CreateNew<ChatServer>();
            server.Name = $"server-{server.Id}";
            server.Description = "Test server";
            server.OwnerId = Guid.NewGuid();
            await serverSaver.SaveAsync(server, ct);
            return server;
        }

        private async Task<ChatChannel> CreateChannelAsync(Guid serverId, string name, CancellationToken ct)
        {
            var channel = Persistable.CreateNew<ChatChannel>();
            channel.ChatServerId = serverId;
            channel.Name = name;
            await channelSaver.SaveAsync(channel, ct);
            return channel;
        }

        private static void AssertMapped(ChatChannel actual, ChatChannel expected)
        {
            actual.Id.Should().Be(expected.Id);
            actual.ChatServerId.Should().Be(expected.ChatServerId);
            actual.Name.Should().Be(expected.Name);
            actual.Deleted.Should().BeFalse();
            // timestamptz stores microseconds while DateTime has 100 ns ticks
            actual.CreatedAt.Should().BeCloseTo(expected.CreatedAt, TimeSpan.FromMilliseconds(1));
            actual.ModifiedAt.Should().BeCloseTo(expected.ModifiedAt, TimeSpan.FromMilliseconds(1));
        }
    }
}
