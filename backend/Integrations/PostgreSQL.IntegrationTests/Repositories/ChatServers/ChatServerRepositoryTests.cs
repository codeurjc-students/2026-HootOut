using Autofac;
using Dapper;
using FluentAssertions;
using HootOut.ChatChannels.Entities;
using HootOut.ChatServers.Entities;
using HootOut.ChatServers.Repositories;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Saver;
using HootOut.Infraestructure.DI;
using HootOut.Users.Entities;

namespace HootOut.PostgreSQL.IntegrationTests.Repositories.ChatServers
{
    [Collection("IntegrationTests")]
    public class ChatServerRepositoryTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private static readonly DateTime BaseDate = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private readonly IContainer container;

        private readonly ClearAllTables clearTables;

        private readonly IChatServerRepository repository;

        private readonly ISaver<ChatServer> serverSaver;

        private readonly ISaver<ServerMember> memberSaver;

        private readonly ISaver<ChatChannel> channelSaver;

        private readonly ISaver<UserInfo> userSaver;

        private readonly IPersistenceProvider provider;

        public ChatServerRepositoryTests(PostgresTestContainer postgresContainer)
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

            repository = container.Resolve<IChatServerRepository>();
            serverSaver = container.Resolve<ISaver<ChatServer>>();
            memberSaver = container.Resolve<ISaver<ServerMember>>();
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

        #region GetServerDtosByUserIdAsync - filtering

        [Fact]
        public async Task GetServerDtosByUserId_UserWithoutServers_ReturnsEmptyPage()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var otherUser = await CreateUserAsync("user2", ct);
            await CreateServerAsync("server", BaseDate, ct, otherUser.Id);

            var result = await repository.GetServerDtosByUserIdAsync(new PageRequest(), user.Id, ct);

            result.Items.Should().BeEmpty();
            result.Next.Should().BeNull();
            result.Total.Should().Be(0);
            result.PageSize.Should().Be(10);
        }

        [Fact]
        public async Task GetServerDtosByUserId_ReturnsMappedServersWithoutChannels()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var server = await CreateServerAsync("server", BaseDate, ct, user.Id);
            await CreateChannelAsync(server.Id, "general", ct);

            var result = await repository.GetServerDtosByUserIdAsync(new PageRequest(), user.Id, ct);

            result.Items.Should().ContainSingle();
            AssertMapped(result.Items[0], server);
            // The list query does not load channels
            result.Items[0].Channels.Should().BeEmpty();
        }

        [Fact]
        public async Task GetServerDtosByUserId_ReturnsOnlyServersWhereUserIsMember()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var otherUser = await CreateUserAsync("user2", ct);
            var shared = await CreateServerAsync("shared", BaseDate, ct, user.Id, otherUser.Id);
            var own = await CreateServerAsync("own", BaseDate.AddMinutes(1), ct, user.Id);
            await CreateServerAsync("other", BaseDate.AddMinutes(2), ct, otherUser.Id);

            var result = await repository.GetServerDtosByUserIdAsync(new PageRequest(), user.Id, ct);

            result.Items.Select(s => s.Id).Should().BeEquivalentTo([shared.Id, own.Id]);
            result.Total.Should().Be(2);
        }

        [Fact]
        public async Task GetServerDtosByUserId_DeletedServer_IsExcludedFromItemsAndTotal()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var kept = await CreateServerAsync("kept", BaseDate, ct, user.Id);
            var deleted = await CreateServerAsync("deleted", BaseDate.AddMinutes(1), ct, user.Id);
            await MarkServerAsDeletedAsync(deleted.Id, ct);

            var result = await repository.GetServerDtosByUserIdAsync(new PageRequest(), user.Id, ct);

            result.Items.Select(s => s.Id).Should().Equal(kept.Id);
            result.Total.Should().Be(1);
        }

        [Fact]
        public async Task GetServerDtosByUserId_DeletedMembership_IsExcludedFromItemsAndTotal()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var kept = await CreateServerAsync("kept", BaseDate, ct, user.Id);
            var left = await CreateServerAsync("left", BaseDate.AddMinutes(1), ct, user.Id);
            await MarkMembershipAsDeletedAsync(left.Id, user.Id, ct);

            var result = await repository.GetServerDtosByUserIdAsync(new PageRequest(), user.Id, ct);

            result.Items.Select(s => s.Id).Should().Equal(kept.Id);
            result.Total.Should().Be(1);
        }

        #endregion

        #region GetServerDtosByUserIdAsync - sorting

        [Fact]
        public async Task GetServerDtosByUserId_DefaultRequest_SortsByCreatedAtDescending()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var oldest = await CreateServerAsync("b", BaseDate, ct, user.Id);
            var newest = await CreateServerAsync("c", BaseDate.AddMinutes(2), ct, user.Id);
            var middle = await CreateServerAsync("a", BaseDate.AddMinutes(1), ct, user.Id);

            var result = await repository.GetServerDtosByUserIdAsync(new PageRequest(), user.Id, ct);

            result.Items.Select(s => s.Id).Should().Equal(newest.Id, middle.Id, oldest.Id);
        }

        [Fact]
        public async Task GetServerDtosByUserId_CreatedAtAscending_SortsOldestFirst()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var oldest = await CreateServerAsync("b", BaseDate, ct, user.Id);
            var newest = await CreateServerAsync("c", BaseDate.AddMinutes(2), ct, user.Id);
            var middle = await CreateServerAsync("a", BaseDate.AddMinutes(1), ct, user.Id);

            var request = new PageRequest { SortBy = "createdAt", Descending = false };
            var result = await repository.GetServerDtosByUserIdAsync(request, user.Id, ct);

            result.Items.Select(s => s.Id).Should().Equal(oldest.Id, middle.Id, newest.Id);
        }

        [Theory]
        [InlineData("name")]
        [InlineData("NAME")]
        [InlineData("Name")]
        public async Task GetServerDtosByUserId_NameAscending_SortsIgnoringCaseAndAccents(string sortBy)
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            await CreateServerAsync("Zeta", BaseDate, ct, user.Id);
            await CreateServerAsync("beta", BaseDate.AddMinutes(1), ct, user.Id);
            await CreateServerAsync("Álvaro", BaseDate.AddMinutes(2), ct, user.Id);
            await CreateServerAsync("alfa", BaseDate.AddMinutes(3), ct, user.Id);

            var request = new PageRequest { SortBy = sortBy, Descending = false };
            var result = await repository.GetServerDtosByUserIdAsync(request, user.Id, ct);

            result.Items.Select(s => s.Name).Should().Equal("alfa", "Álvaro", "beta", "Zeta");
        }

        [Fact]
        public async Task GetServerDtosByUserId_NameDescending_SortsIgnoringCaseAndAccents()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            await CreateServerAsync("Zeta", BaseDate, ct, user.Id);
            await CreateServerAsync("beta", BaseDate.AddMinutes(1), ct, user.Id);
            await CreateServerAsync("Álvaro", BaseDate.AddMinutes(2), ct, user.Id);
            await CreateServerAsync("alfa", BaseDate.AddMinutes(3), ct, user.Id);

            var request = new PageRequest { SortBy = "name", Descending = true };
            var result = await repository.GetServerDtosByUserIdAsync(request, user.Id, ct);

            result.Items.Select(s => s.Name).Should().Equal("Zeta", "beta", "Álvaro", "alfa");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("unknown")]
        [InlineData("Id; DROP TABLE \"HootOut\".\"ChatServer\"")]
        public async Task GetServerDtosByUserId_UnknownSortBy_FallsBackToCreatedAt(string? sortBy)
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var oldest = await CreateServerAsync("a", BaseDate, ct, user.Id);
            var newest = await CreateServerAsync("b", BaseDate.AddMinutes(1), ct, user.Id);

            var request = new PageRequest { SortBy = sortBy, Descending = true };
            var result = await repository.GetServerDtosByUserIdAsync(request, user.Id, ct);

            result.Items.Select(s => s.Id).Should().Equal(newest.Id, oldest.Id);
        }

        #endregion

        #region GetServerDtosByUserIdAsync - pagination

        [Fact]
        public async Task GetServerDtosByUserId_FirstPage_ReturnsPageSizeItemsAndNextCursor()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            await CreateServersAsync(user.Id, 5, ct);

            var request = new PageRequest { PageSize = 2 };
            var result = await repository.GetServerDtosByUserIdAsync(request, user.Id, ct);

            result.Items.Should().HaveCount(2);
            result.Next.Should().Be(result.Items[^1].Id);
            result.PageSize.Should().Be(2);
            // Total ignores pagination
            result.Total.Should().Be(5);
        }

        [Fact]
        public async Task GetServerDtosByUserId_ExactlyPageSizeItems_ReturnsNoNextCursor()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            await CreateServersAsync(user.Id, 2, ct);

            var request = new PageRequest { PageSize = 2 };
            var result = await repository.GetServerDtosByUserIdAsync(request, user.Id, ct);

            result.Items.Should().HaveCount(2);
            result.Next.Should().BeNull();
        }

        [Theory]
        [InlineData("createdat", true)]
        [InlineData("createdat", false)]
        [InlineData("name", true)]
        [InlineData("name", false)]
        public async Task GetServerDtosByUserId_WalkingAllPages_ReturnsEveryServerOnceInOrder(string sortBy, bool descending)
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            await CreateServersAsync(user.Id, 7, ct);

            var expected = await GetAllInOnePageAsync(user.Id, sortBy, descending, ct);
            var pages = await WalkAllPagesAsync(user.Id, new PageRequest { SortBy = sortBy, Descending = descending, PageSize = 3 }, ct);

            pages.Should().HaveCount(3);
            pages.Select(p => p.Items.Count).Should().Equal(3, 3, 1);
            pages.Should().OnlyContain(p => p.Total == 7);
            pages.SelectMany(p => p.Items).Select(s => s.Id).Should().Equal(expected);
        }

        [Theory]
        [InlineData("createdat", true)]
        [InlineData("createdat", false)]
        [InlineData("name", true)]
        [InlineData("name", false)]
        public async Task GetServerDtosByUserId_TiedSortValues_UsesIdAsTieBreakerWithoutLosingRows(string sortBy, bool descending)
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var created = new List<Guid>();
            for (var i = 0; i < 5; i++)
            {
                // Same name and same CreatedAt for every server
                var server = await CreateServerAsync("same", BaseDate, ct, user.Id);
                created.Add(server.Id);
            }

            var expected = await GetAllInOnePageAsync(user.Id, sortBy, descending, ct);
            var pages = await WalkAllPagesAsync(user.Id, new PageRequest { SortBy = sortBy, Descending = descending, PageSize = 2 }, ct);

            var walked = pages.SelectMany(p => p.Items).Select(s => s.Id).ToList();
            walked.Should().OnlyHaveUniqueItems();
            walked.Should().BeEquivalentTo(created);
            walked.Should().Equal(expected);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(-5, 1)]
        [InlineData(100, 50)]
        public async Task GetServerDtosByUserId_OutOfRangePageSize_IsClamped(int pageSize, int expectedSize)
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            await CreateServersAsync(user.Id, 2, ct);

            var request = new PageRequest { PageSize = pageSize };
            var result = await repository.GetServerDtosByUserIdAsync(request, user.Id, ct);

            result.PageSize.Should().Be(expectedSize);
            result.Items.Should().HaveCount(Math.Min(2, expectedSize));
        }

        [Fact]
        public async Task GetServerDtosByUserId_CursorFromServerUserIsNotMemberOf_ReturnsEmptyItems()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var otherUser = await CreateUserAsync("user2", ct);
            await CreateServersAsync(user.Id, 3, ct);
            var foreign = await CreateServerAsync("foreign", BaseDate, ct, otherUser.Id);

            var request = new PageRequest { Next = foreign.Id };
            var result = await repository.GetServerDtosByUserIdAsync(request, user.Id, ct);

            // The cursor subquery returns no row, so the row comparison is NULL and filters everything out
            result.Items.Should().BeEmpty();
            result.Next.Should().BeNull();
            result.Total.Should().Be(3);
        }

        #endregion

        #region GetServerDtoByIdAsync

        [Fact]
        public async Task GetServerDtoById_NotExisting_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            await CreateServerAsync("server", BaseDate, ct, user.Id);

            var result = await repository.GetServerDtoByIdAsync(Guid.NewGuid(), ct);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetServerDtoById_Deleted_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var server = await CreateServerAsync("server", BaseDate, ct, user.Id);
            await CreateChannelAsync(server.Id, "general", ct);
            await MarkServerAsDeletedAsync(server.Id, ct);

            var result = await repository.GetServerDtoByIdAsync(server.Id, ct);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetServerDtoById_WithoutChannels_ReturnsServerWithEmptyChannels()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var server = await CreateServerAsync("server", BaseDate, ct, user.Id);

            var result = await repository.GetServerDtoByIdAsync(server.Id, ct);

            result.Should().NotBeNull();
            AssertMapped(result, server);
            result.Channels.Should().BeEmpty();
        }

        [Fact]
        public async Task GetServerDtoById_WithoutMembers_StillReturnsServer()
        {
            var ct = TestContext.Current.CancellationToken;
            var server = await CreateServerAsync("server", BaseDate, ct);

            var result = await repository.GetServerDtoByIdAsync(server.Id, ct);

            result.Should().NotBeNull();
            result.Id.Should().Be(server.Id);
        }

        [Fact]
        public async Task GetServerDtoById_WithChannels_ReturnsChannelsOrderedByName()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var server = await CreateServerAsync("server", BaseDate, ct, user.Id);
            var charlie = await CreateChannelAsync(server.Id, "charlie", ct);
            var alpha = await CreateChannelAsync(server.Id, "alpha", ct);
            var bravo = await CreateChannelAsync(server.Id, "bravo", ct);

            var result = await repository.GetServerDtoByIdAsync(server.Id, ct);

            result.Should().NotBeNull();
            AssertMapped(result, server);
            result.Channels.Select(c => c.Id).Should().Equal(alpha.Id, bravo.Id, charlie.Id);
            result.Channels.Select(c => c.Name).Should().Equal("alpha", "bravo", "charlie");
            result.Channels.Should().OnlyContain(c => c.ChatServerId == server.Id);
        }

        [Fact]
        public async Task GetServerDtoById_DeletedChannel_IsExcluded()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var server = await CreateServerAsync("server", BaseDate, ct, user.Id);
            var kept = await CreateChannelAsync(server.Id, "general", ct);
            var deleted = await CreateChannelAsync(server.Id, "random", ct);
            await channelSaver.DeleteAsync(deleted, ct);

            var result = await repository.GetServerDtoByIdAsync(server.Id, ct);

            result.Should().NotBeNull();
            result.Channels.Select(c => c.Id).Should().Equal(kept.Id);
        }

        [Fact]
        public async Task GetServerDtoById_OnlyDeletedChannels_ReturnsServerWithEmptyChannels()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var server = await CreateServerAsync("server", BaseDate, ct, user.Id);
            var deleted = await CreateChannelAsync(server.Id, "general", ct);
            await channelSaver.DeleteAsync(deleted, ct);

            var result = await repository.GetServerDtoByIdAsync(server.Id, ct);

            result.Should().NotBeNull();
            result.Id.Should().Be(server.Id);
            result.Channels.Should().BeEmpty();
        }

        [Fact]
        public async Task GetServerDtoById_DoesNotIncludeChannelsFromOtherServers()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var server = await CreateServerAsync("server", BaseDate, ct, user.Id);
            var otherServer = await CreateServerAsync("other", BaseDate, ct, user.Id);
            var own = await CreateChannelAsync(server.Id, "general", ct);
            await CreateChannelAsync(otherServer.Id, "general", ct);

            var result = await repository.GetServerDtoByIdAsync(server.Id, ct);

            result.Should().NotBeNull();
            result.Channels.Select(c => c.Id).Should().Equal(own.Id);
        }

        #endregion

        #region Cancellation

        [Theory]
        [InlineData(nameof(IChatServerRepository.GetServerDtosByUserIdAsync))]
        [InlineData(nameof(IChatServerRepository.GetServerDtoByIdAsync))]
        public async Task AnyMethod_CancelledToken_ThrowsOperationCanceled(string method)
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user1", ct);
            var server = await CreateServerAsync("server", BaseDate, ct, user.Id);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            Func<Task> act = method switch
            {
                nameof(IChatServerRepository.GetServerDtosByUserIdAsync) => () => repository.GetServerDtosByUserIdAsync(new PageRequest(), user.Id, cts.Token),
                nameof(IChatServerRepository.GetServerDtoByIdAsync) => () => repository.GetServerDtoByIdAsync(server.Id, cts.Token),
                _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
            };

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        #endregion

        private async Task<UserInfo> CreateUserAsync(string username, CancellationToken ct)
        {
            var user = Persistable.CreateNew<UserInfo>();
            user.Username = username;
            user.Email = $"{username}@test.com";
            user.Password = $"hash-{username}";
            await userSaver.SaveAsync(user, ct);
            return user;
        }

        private async Task<ChatServer> CreateServerAsync(string name, DateTime createdAt, CancellationToken ct, params Guid[] memberIds)
        {
            var server = Persistable.CreateNew<ChatServer>();
            server.Name = name;
            server.Description = $"Description of {name}";
            server.OwnerId = memberIds.Length > 0 ? memberIds[0] : Guid.NewGuid();
            server.CreatedAt = createdAt;
            server.ModifiedAt = createdAt;
            await serverSaver.SaveAsync(server, ct);

            foreach (var memberId in memberIds)
            {
                var member = Persistable.CreateNew<ServerMember>();
                member.ChatServerId = server.Id;
                member.UserId = memberId;
                await memberSaver.SaveAsync(member, ct);
            }

            return server;
        }

        /// <summary>
        /// Creates servers with distinct names and creation dates; name order is the reverse of creation order.
        /// </summary>
        private async Task CreateServersAsync(Guid userId, int count, CancellationToken ct)
        {
            for (var i = 0; i < count; i++)
            {
                var name = $"server-{count - i:D2}";
                await CreateServerAsync(name, BaseDate.AddMinutes(i), ct, userId);
            }
        }

        private async Task<ChatChannel> CreateChannelAsync(Guid serverId, string name, CancellationToken ct)
        {
            var channel = Persistable.CreateNew<ChatChannel>();
            channel.ChatServerId = serverId;
            channel.Name = name;
            await channelSaver.SaveAsync(channel, ct);
            return channel;
        }

        private async Task<List<Guid>> GetAllInOnePageAsync(Guid userId, string sortBy, bool descending, CancellationToken ct)
        {
            var request = new PageRequest { SortBy = sortBy, Descending = descending, PageSize = 50 };
            var result = await repository.GetServerDtosByUserIdAsync(request, userId, ct);
            result.Next.Should().BeNull();
            return result.Items.Select(s => s.Id).ToList();
        }

        private async Task<List<PageResult<ChatServerDto>>> WalkAllPagesAsync(Guid userId, PageRequest firstRequest, CancellationToken ct)
        {
            var pages = new List<PageResult<ChatServerDto>>();
            var request = firstRequest;

            // Upper bound to avoid an infinite loop if the cursor never ends
            for (var i = 0; i < 20; i++)
            {
                var page = await repository.GetServerDtosByUserIdAsync(request, userId, ct);
                pages.Add(page);

                if (page.Next is null)
                    return pages;

                request = request with { Next = page.Next };
            }

            throw new InvalidOperationException("Pagination did not finish after 20 pages");
        }

        private async Task MarkServerAsDeletedAsync(Guid serverId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                """UPDATE "HootOut"."ChatServer" SET "Deleted" = true WHERE "Id" = @Id""",
                new { Id = serverId },
                cancellationToken: ct));
        }

        private async Task MarkMembershipAsDeletedAsync(Guid serverId, Guid userId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                """UPDATE "HootOut"."ServerMember" SET "Deleted" = true WHERE "ChatServerId" = @ServerId AND "UserId" = @UserId""",
                new { ServerId = serverId, UserId = userId },
                cancellationToken: ct));
        }

        private static void AssertMapped(ChatServerDto actual, ChatServer expected)
        {
            actual.Id.Should().Be(expected.Id);
            actual.Name.Should().Be(expected.Name);
            actual.Description.Should().Be(expected.Description);
            actual.OwnerId.Should().Be(expected.OwnerId);
            // timestamptz stores microseconds while DateTime has 100 ns ticks
            actual.CreatedAt.Should().BeCloseTo(expected.CreatedAt, TimeSpan.FromMilliseconds(1));
            actual.ModifiedAt.Should().BeCloseTo(expected.ModifiedAt, TimeSpan.FromMilliseconds(1));
        }
    }
}
