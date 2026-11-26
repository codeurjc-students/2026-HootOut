using Autofac;
using Dapper;
using FluentAssertions;
using HootOut.ChatServers.Entities;
using HootOut.ChatServers.Repositories;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Saver;
using HootOut.Infraestructure.DI;
using HootOut.Users.Entities;

namespace HootOut.PostgreSQL.IntegrationTests.Repositories.ChatServers
{
    /// <summary>The queries used for the online / offline members (same setup as ServerMemberRepositoryTests).</summary>
    [Collection("IntegrationTests")]
    public class ServerMemberRepositoryPresenceTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private static readonly DateTime BaseDate = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private readonly IContainer container;
        private readonly ClearAllTables clearTables;
        private readonly IServerMemberRepository repository;
        private readonly ISaver<ServerMember> memberSaver;
        private readonly ISaver<ChatServer> serverSaver;
        private readonly ISaver<UserInfo> userSaver;

        public ServerMemberRepositoryPresenceTests(PostgresTestContainer postgresContainer)
        {
            var builder = new ContainerBuilder();
            new RegistrationManager().RegisterAllAssemblies(builder);
            builder.RegisterInstance(postgresContainer.postgreSQLProvider!).As<IPersistenceProvider>().SingleInstance();
            container = builder.Build();

            clearTables = container.Resolve<ClearAllTables>();
            foreach (var value in container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority))
            {
                value.InitAsync().Wait();
            }

            repository = container.Resolve<IServerMemberRepository>();
            memberSaver = container.Resolve<ISaver<ServerMember>>();
            serverSaver = container.Resolve<ISaver<ChatServer>>();
            userSaver = container.Resolve<ISaver<UserInfo>>();
        }

        public async ValueTask InitializeAsync() => await clearTables.ClearTablesAsync();

        public async ValueTask DisposeAsync()
        {
            await clearTables.ClearTablesAsync();
            GC.SuppressFinalize(this);
        }

        #region GetServerMemberDtosAsync with excluded users

        [Fact]
        public async Task GetServerMemberDtos_NoExcludedUsers_SameAsWithoutThem()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, _) = await CreateServerWithMembersAsync(["a", "b", "c"], ct);
            var request = new PageRequest { SortBy = "username", Descending = false };

            var withEmpty = await repository.GetServerMemberDtosAsync(server.Id, request, [], ct);
            var without = await repository.GetServerMemberDtosAsync(server.Id, request, ct);

            withEmpty.Items.Select(m => m.Id).Should().Equal(without.Items.Select(m => m.Id));
            withEmpty.Total.Should().Be(without.Total);
        }

        [Fact]
        public async Task GetServerMemberDtos_ExcludedUsers_AreNotInItemsOrTotal()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerWithMembersAsync(["a", "b", "c", "d"], ct);

            var result = await repository.GetServerMemberDtosAsync(server.Id, new PageRequest { SortBy = "username", Descending = false },
                [members[1].UserId, members[3].UserId, Guid.NewGuid()], ct);

            result.Items.Select(m => m.Username).Should().Equal("a", "c");
            result.Total.Should().Be(2);
        }

        [Fact]
        public async Task GetServerMemberDtos_ExcludedCursorMember_StillWorksAsPosition()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerWithMembersAsync(["a", "b", "c", "d"], ct);
            var request = new PageRequest { SortBy = "username", Descending = false, PageSize = 2 };
            var first = await repository.GetServerMemberDtosAsync(server.Id, request, [], ct);

            // "b" is the cursor and is excluded in the next request
            var second = await repository.GetServerMemberDtosAsync(server.Id, request with { Next = first.Next }, [members[1].UserId], ct);

            first.Items.Select(m => m.Username).Should().Equal("a", "b");
            second.Items.Select(m => m.Username).Should().Equal("c", "d");
            second.Next.Should().BeNull();
        }

        [Fact]
        public async Task GetServerMemberDtos_EveryMemberExcluded_ReturnsEmptyPage()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerWithMembersAsync(["a", "b"], ct);

            var result = await repository.GetServerMemberDtosAsync(server.Id, new PageRequest(), members.Select(m => m.UserId).ToList(), ct);

            result.Items.Should().BeEmpty();
            result.Total.Should().Be(0);
            result.Next.Should().BeNull();
        }

        #endregion

        #region GetServerMemberDtosByUserIdsAsync

        [Fact]
        public async Task GetByUserIds_ReturnsTheMembersSortedByUsername()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerWithMembersAsync(["Zeta", "beta", "Álvaro", "alfa", "other"], ct);

            var result = await repository.GetServerMemberDtosByUserIdsAsync(server.Id, members.Take(4).Select(m => m.UserId).ToList(), 50, ct);

            result.Select(m => m.Username).Should().Equal("alfa", "Álvaro", "beta", "Zeta");
            result[0].Id.Should().Be(members[3].Id);
            result[0].UserId.Should().Be(members[3].UserId);
            result[0].CreatedAt.Should().BeCloseTo(members[3].CreatedAt, TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public async Task GetByUserIds_EmptyList_ReturnsEmpty()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, _) = await CreateServerWithMembersAsync(["a"], ct);

            (await repository.GetServerMemberDtosByUserIdsAsync(server.Id, [], 50, ct)).Should().BeEmpty();
        }

        [Fact]
        public async Task GetByUserIds_RespectsTheLimit()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerWithMembersAsync(["d", "c", "b", "a"], ct);

            var result = await repository.GetServerMemberDtosByUserIdsAsync(server.Id, members.Select(m => m.UserId).ToList(), 2, ct);

            result.Select(m => m.Username).Should().Equal("a", "b");
        }

        [Fact]
        public async Task GetByUserIds_IgnoresUsersOfOtherServersAndDeletedMembers()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerWithMembersAsync(["a"], ct);
            var outsider = await CreateUserAsync("outsider", ct);
            var otherServer = await CreateServerAsync("other", outsider.Id, ct);
            await AddMemberAsync(otherServer.Id, outsider.Id, BaseDate, ct);
            var left = await CreateUserAsync("left", ct);
            var leftMember = await AddMemberAsync(server.Id, left.Id, BaseDate, ct);
            await MarkMemberAsDeletedAsync(leftMember.Id, ct);

            var result = await repository.GetServerMemberDtosByUserIdsAsync(server.Id, [members[0].UserId, outsider.Id, left.Id], 50, ct);

            result.Select(m => m.Username).Should().Equal("a");
        }

        #endregion

        #region Helpers

        private async Task<UserInfo> CreateUserAsync(string username, CancellationToken ct)
        {
            var user = Persistable.CreateNew<UserInfo>();
            user.Username = username;
            user.Email = $"{username}@test.com";
            user.Password = $"hash-{username}";
            await userSaver.SaveAsync(user, ct);
            return user;
        }

        private async Task<ChatServer> CreateServerAsync(string name, Guid ownerId, CancellationToken ct)
        {
            var server = Persistable.CreateNew<ChatServer>();
            server.Name = name;
            server.Description = $"Description of {name}";
            server.OwnerId = ownerId;
            await serverSaver.SaveAsync(server, ct);
            return server;
        }

        private async Task<ServerMember> AddMemberAsync(Guid serverId, Guid userId, DateTime createdAt, CancellationToken ct)
        {
            var member = Persistable.CreateNew<ServerMember>();
            member.ChatServerId = serverId;
            member.UserId = userId;
            member.CreatedAt = createdAt;
            member.ModifiedAt = createdAt;
            await memberSaver.SaveAsync(member, ct);
            return member;
        }

        private async Task<(ChatServer Server, List<ServerMember> Members)> CreateServerWithMembersAsync(string[] usernames, CancellationToken ct)
        {
            var owner = await CreateUserAsync("owner", ct);
            var server = await CreateServerAsync("server", owner.Id, ct);
            var members = new List<ServerMember>();
            for (var i = 0; i < usernames.Length; i++)
            {
                var user = await CreateUserAsync(usernames[i], ct);
                members.Add(await AddMemberAsync(server.Id, user.Id, BaseDate.AddMinutes(i), ct));
            }
            return (server, members);
        }

        private async Task MarkMemberAsDeletedAsync(Guid memberId, CancellationToken ct)
        {
            var provider = container.Resolve<IPersistenceProvider>();
            await using var conn = await provider.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                """UPDATE "HootOut"."ServerMember" SET "Deleted" = true WHERE "Id" = @Id""",
                new { Id = memberId },
                cancellationToken: ct));
        }

        #endregion
    }
}
