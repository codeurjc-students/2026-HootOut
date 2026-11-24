using Autofac;
using Dapper;
using FluentAssertions;
using FluentValidation;
using HootOut.ChatServers.Entities;
using HootOut.ChatServers.Repositories;
using HootOut.ChatServers.Services;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.CommonIntegrationTests.Services;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.ChatServers.Requests;
using HootOut.Contracts.ChatServers.Services;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Presence;
using HootOut.Contracts.Common.Saver;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.Contracts.Users.Services;
using HootOut.Redis.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace HootOut.ChatServers.IntegrationTests.Services
{
    /// <summary>
    /// Online / offline members: Postgres for the members, Valkey (real RedisPresenceStore) for who is online.
    /// The test project needs a reference to HootOut.Redis (CommonIntegrationTests already has it).
    /// </summary>
    [Collection("IntegrationTests")]
    public class ServerMemberPresenceServiceTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IClassFixture<TestContainerRedisConnectionFactory>, IAsyncLifetime
    {
        private static readonly DateTime BaseDate = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private readonly IContainer container;
        private readonly ClearAllTables clearTables;
        private readonly TestContainerRedisConnectionFactory redisFactory;
        private readonly IPersistenceProvider provider;

        private readonly IServerMemberRepository memberRepository;
        private readonly IChatServerService chatServerService;
        private readonly ISaver<ServerMember> memberSaver;
        private readonly RedisPresenceStore presenceStore;
        private readonly ServerMemberPresenceService service;

        public ServerMemberPresenceServiceTests(PostgresTestContainer postgresContainer, TestContainerRedisConnectionFactory redisFactory)
        {
            this.redisFactory = redisFactory;

            var builder = postgresContainer.RegisterTestPersistenceProvider();
            container = builder.Build();

            provider = container.Resolve<IPersistenceProvider>();
            clearTables = container.Resolve<ClearAllTables>();
            foreach (var value in container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority))
            {
                value.InitAsync().Wait();
            }

            memberRepository = container.Resolve<IServerMemberRepository>();
            chatServerService = container.Resolve<IChatServerService>();
            memberSaver = container.Resolve<ISaver<ServerMember>>();

            presenceStore = new RedisPresenceStore(redisFactory.Connect(), new PresenceInstance(), new PresenceOptions());
            service = CreateService(presenceStore);
        }

        public async ValueTask InitializeAsync()
        {
            await clearTables.ClearTablesAsync();
            await redisFactory.FlushAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await clearTables.ClearTablesAsync();
            GC.SuppressFinalize(this);
        }

        private ServerMemberPresenceService CreateService(IPresenceStore store) =>
            new(NullLogger<ServerMemberPresenceService>.Instance, memberRepository, new Lazy<IPresenceStore>(() => store));

        #region Validation

        [Fact]
        public async Task GetOnlineMembers_NotAMember_ThrowsValidation()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, _) = await CreateServerAsync(["a"], ct);
            var outsider = await CreateUserAsync("outsider", ct);

            var act = () => service.GetOnlineMembersAsync(server, outsider, ct);

            await act.Should().ThrowAsync<ValidationException>();
        }

        [Fact]
        public async Task GetOfflineMembers_NotAMember_ThrowsValidation()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, _) = await CreateServerAsync(["a"], ct);
            var outsider = await CreateUserAsync("outsider", ct);

            var act = () => service.GetOfflineMembersAsync(server, outsider, new PageRequest(), ct);

            await act.Should().ThrowAsync<ValidationException>();
        }

        [Fact]
        public async Task GetOnlineMembers_DeletedMember_ThrowsValidation()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerAsync(["a"], ct);
            await MarkMemberAsDeletedAsync(server, members["a"], ct);

            var act = () => service.GetOnlineMembersAsync(server, members["a"], ct);

            await act.Should().ThrowAsync<ValidationException>();
        }

        [Fact]
        public async Task GetOnlineMembers_ServerDoesNotExist_ThrowsValidation()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);

            var act = () => service.GetOnlineMembersAsync(Guid.NewGuid(), user, ct);

            await act.Should().ThrowAsync<ValidationException>();
        }

        #endregion

        #region Online

        [Fact]
        public async Task GetOnlineMembers_NobodyOnline_ReturnsEmpty()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerAsync(["a", "b"], ct);

            var result = await service.GetOnlineMembersAsync(server, members["owner"], ct);

            result.Items.Should().BeEmpty();
            result.Total.Should().Be(0);
        }

        [Fact]
        public async Task GetOnlineMembers_ReturnsOnlyOnlineMembersSortedByUsername()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerAsync(["Zeta", "beta", "Álvaro", "alfa", "offline"], ct);
            await ConnectAsync(members["Zeta"], members["beta"], members["Álvaro"], members["alfa"]);

            var result = await service.GetOnlineMembersAsync(server, members["owner"], ct);

            result.Items.Select(m => m.Username).Should().Equal("alfa", "Álvaro", "beta", "Zeta");
            result.Total.Should().Be(4);
            var alfa = result.Items[0];
            alfa.UserId.Should().Be(members["alfa"]);
            alfa.Id.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GetOnlineMembers_OnlineUserOfAnotherServer_IsNotIncluded()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerAsync(["a"], ct);
            var (_, others) = await CreateServerAsync(["other"], ct, ownerName: "owner2");
            await ConnectAsync(members["a"], others["other"]);

            var result = await service.GetOnlineMembersAsync(server, members["owner"], ct);

            result.Items.Select(m => m.UserId).Should().Equal(members["a"]);
            result.Total.Should().Be(1);
        }

        [Fact]
        public async Task GetOnlineMembers_DeletedMemberOnline_IsNotIncluded()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerAsync(["kept", "left"], ct);
            await MarkMemberAsDeletedAsync(server, members["left"], ct);
            await ConnectAsync(members["kept"], members["left"]);

            var result = await service.GetOnlineMembersAsync(server, members["owner"], ct);

            result.Items.Select(m => m.Username).Should().Equal("kept");
            result.Total.Should().Be(1);
        }

        [Fact]
        public async Task GetOnlineMembers_DisconnectedMember_IsNotIncluded()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerAsync(["a", "b"], ct);
            await ConnectAsync(members["a"]);
            var connection = Guid.NewGuid();
            await presenceStore.AddConnectionAsync(members["b"], connection, ct);
            await presenceStore.RemoveConnectionAsync(members["b"], connection, ct);

            var result = await service.GetOnlineMembersAsync(server, members["owner"], ct);

            result.Items.Select(m => m.Username).Should().Equal("a");
        }

        [Fact]
        public async Task GetOnlineMembers_MoreThanTheLimit_CapsItemsAndKeepsTotal()
        {
            var ct = TestContext.Current.CancellationToken;
            var count = ServerMemberPresenceService.MaxOnlineMembers + 5;
            var names = Enumerable.Range(0, count).Select(i => $"user{i:D4}").ToArray();
            var (server, members) = await CreateServerAsync(names, ct);
            await ConnectAsync(names.Select(n => members[n]).ToArray());

            var result = await service.GetOnlineMembersAsync(server, members["owner"], ct);

            result.Items.Should().HaveCount(ServerMemberPresenceService.MaxOnlineMembers);
            result.Items.Select(m => m.Username).Should().Equal(names.Take(ServerMemberPresenceService.MaxOnlineMembers));
            result.Total.Should().Be(count);
        }

        [Fact]
        public async Task GetOnlineMembers_ValkeyFails_ReturnsEmpty()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerAsync(["a"], ct);
            await ConnectAsync(members["a"]);

            var result = await CreateService(new FailingPresenceStore()).GetOnlineMembersAsync(server, members["owner"], ct);

            result.Items.Should().BeEmpty();
            result.Total.Should().Be(0);
        }

        [Fact]
        public async Task GetOnlineMembers_StoreCannotBeCreated_ReturnsEmpty()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerAsync(["a"], ct);
            // E.g. REDIS_HOST is not defined: the factory of the multiplexer throws
            var broken = new ServerMemberPresenceService(NullLogger<ServerMemberPresenceService>.Instance, memberRepository,
                new Lazy<IPresenceStore>(() => throw new InvalidOperationException("REDIS_HOST is not defined")));

            var result = await broken.GetOnlineMembersAsync(server, members["owner"], ct);

            result.Items.Should().BeEmpty();
        }

        #endregion

        #region Offline

        [Fact]
        public async Task GetOfflineMembers_ExcludesOnlineMembersFromItemsAndTotal()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerAsync(["a", "b", "c"], ct);
            await ConnectAsync(members["owner"], members["b"]);

            var result = await service.GetOfflineMembersAsync(server, members["owner"], new PageRequest { Descending = false }, ct);

            result.Items.Select(m => m.Username).Should().Equal("a", "c");
            result.Total.Should().Be(2);
            result.Next.Should().BeNull();
        }

        [Fact]
        public async Task GetOfflineMembers_WithoutSortBy_SortsByUsernameAscending()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerAsync(["Zeta", "beta", "Álvaro", "alfa"], ct);
            await ConnectAsync(members["owner"]);

            // The controller sends Descending = false when the query string does not have it
            var result = await service.GetOfflineMembersAsync(server, members["owner"], new PageRequest { Descending = false }, ct);

            result.Items.Select(m => m.Username).Should().Equal("alfa", "Álvaro", "beta", "Zeta");
        }

        [Fact]
        public async Task GetOfflineMembers_WithSortBy_UsesIt()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerAsync(["b", "c", "a"], ct);
            await ConnectAsync(members["owner"]);

            var request = new PageRequest { SortBy = "createdAt", Descending = true };
            var result = await service.GetOfflineMembersAsync(server, members["owner"], request, ct);

            // Members join one minute apart, in the given order
            result.Items.Select(m => m.Username).Should().Equal("a", "c", "b");
        }

        [Fact]
        public async Task GetOfflineMembers_WalkingAllPages_ReturnsEveryOfflineMemberOnceInOrder()
        {
            var ct = TestContext.Current.CancellationToken;
            var names = new[] { "u1", "u2", "u3", "u4", "u5", "u6", "u7", "u8" };
            var (server, members) = await CreateServerAsync(names, ct);
            await ConnectAsync(members["owner"], members["u2"], members["u5"]);

            var walked = new List<ServerMemberDto>();
            var request = new PageRequest { PageSize = 2, Descending = false };
            for (var i = 0; i < 10; i++)
            {
                var page = await service.GetOfflineMembersAsync(server, members["owner"], request, ct);
                page.Total.Should().Be(6);
                walked.AddRange(page.Items);
                if (page.Next is null)
                    break;
                request = request with { Next = page.Next };
            }

            walked.Select(m => m.Username).Should().Equal("u1", "u3", "u4", "u6", "u7", "u8");
        }

        [Fact]
        public async Task GetOfflineMembers_MemberGoesOnlineBetweenPages_IsNotRepeatedOrBreaksTheCursor()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerAsync(["u1", "u2", "u3", "u4"], ct);
            await ConnectAsync(members["owner"]);

            var first = await service.GetOfflineMembersAsync(server, members["owner"], new PageRequest { PageSize = 2, Descending = false }, ct);
            first.Items.Select(m => m.Username).Should().Equal("u1", "u2");

            // The last member of the first page (the cursor) goes online
            await ConnectAsync(members["u2"]);

            var second = await service.GetOfflineMembersAsync(server, members["owner"], new PageRequest { PageSize = 2, Descending = false, Next = first.Next }, ct);
            second.Items.Select(m => m.Username).Should().Equal("u3", "u4");
            second.Total.Should().Be(3);
        }

        [Fact]
        public async Task GetOfflineMembers_ValkeyFails_ReturnsEveryMember()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerAsync(["a"], ct);
            await ConnectAsync(members["a"]);

            var result = await CreateService(new FailingPresenceStore()).GetOfflineMembersAsync(server, members["owner"], new PageRequest { Descending = false }, ct);

            result.Items.Select(m => m.Username).Should().Equal("a", "owner");
        }

        [Fact]
        public async Task OnlineAndOffline_TogetherContainEveryMemberOnce()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerAsync(["a", "b", "c", "d"], ct);
            await ConnectAsync(members["a"], members["c"]);

            var online = await service.GetOnlineMembersAsync(server, members["owner"], ct);
            var offline = await service.GetOfflineMembersAsync(server, members["owner"], new PageRequest { PageSize = 50 }, ct);

            online.Items.Select(m => m.UserId).Concat(offline.Items.Select(m => m.UserId))
                .Should().BeEquivalentTo(members.Values);
            (online.Total + offline.Total).Should().Be(members.Count);
        }

        #endregion

        #region Helpers

        private async Task ConnectAsync(params Guid[] userIds)
        {
            foreach (var userId in userIds)
            {
                await presenceStore.AddConnectionAsync(userId, Guid.NewGuid());
            }
        }

        private async Task<Guid> CreateUserAsync(string username, CancellationToken ct)
        {
            var userService = container.Resolve<IUserService>();
            var user = await userService.CreateUserAsync(new CreateUserRequest
            {
                Email = $"{username}-{Guid.NewGuid()}@test.com",
                Username = username,
                Password = Guid.NewGuid().ToString(),
            });
            return user.Id;
        }

        /// <summary>
        /// Creates a server (its owner is a member called <paramref name="ownerName"/>) and one member per username,
        /// joining one minute apart in the given order. Returns the user ids by username, owner included.
        /// </summary>
        private async Task<(Guid Server, Dictionary<string, Guid> Members)> CreateServerAsync(string[] usernames, CancellationToken ct, string ownerName = "owner")
        {
            var owner = await CreateUserAsync(ownerName, ct);
            var server = await chatServerService.CreateChatServerAsync(new CreateChatServerRequest { Name = $"server-{ownerName}" }, owner, ct);
            var members = new Dictionary<string, Guid> { [ownerName] = owner };

            for (var i = 0; i < usernames.Length; i++)
            {
                var userId = await CreateUserAsync(usernames[i], ct);
                var member = Persistable.CreateNew<ServerMember>();
                member.ChatServerId = server.Id;
                member.UserId = userId;
                member.CreatedAt = BaseDate.AddMinutes(i);
                member.ModifiedAt = member.CreatedAt;
                await memberSaver.SaveAsync(member, ct);
                members[usernames[i]] = userId;
            }

            return (server.Id, members);
        }

        private async Task MarkMemberAsDeletedAsync(Guid serverId, Guid userId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                """UPDATE "HootOut"."ServerMember" SET "Deleted" = true WHERE "ChatServerId" = @ServerId AND "UserId" = @UserId""",
                new { ServerId = serverId, UserId = userId },
                cancellationToken: ct));
        }

        /// <summary>Valkey not reachable.</summary>
        private sealed class FailingPresenceStore : IPresenceStore
        {
            private static Exception Fail() => new InvalidOperationException("Valkey is down");

            public Task<bool> AddConnectionAsync(Guid userId, Guid connectionId, CancellationToken ct = default) => throw Fail();
            public Task<bool> RemoveConnectionAsync(Guid userId, Guid connectionId, CancellationToken ct = default) => throw Fail();
            public Task<bool> IsOnlineAsync(Guid userId, CancellationToken ct = default) => throw Fail();
            public Task<IReadOnlySet<Guid>> GetOnlineUsersAsync(IEnumerable<Guid> userIds, CancellationToken ct = default) => throw Fail();
            public Task RegisterInstanceAsync(CancellationToken ct = default) => throw Fail();
            public Task<bool> HeartbeatAsync(CancellationToken ct = default) => throw Fail();
            public Task<IReadOnlyCollection<Guid>> SweepDeadInstancesAsync(CancellationToken ct = default) => throw Fail();
        }

        #endregion
    }
}
