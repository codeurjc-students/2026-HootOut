using Autofac;
using Dapper;
using FluentAssertions;
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
    public class ServerMemberRepositoryTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private static readonly DateTime BaseDate = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private readonly IContainer container;

        private readonly ClearAllTables clearTables;

        private readonly IServerMemberRepository repository;

        private readonly ISaver<ServerMember> memberSaver;

        private readonly ISaver<ChatServer> serverSaver;

        private readonly ISaver<UserInfo> userSaver;

        private readonly IPersistenceProvider provider;

        public ServerMemberRepositoryTests(PostgresTestContainer postgresContainer)
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

            repository = container.Resolve<IServerMemberRepository>();
            memberSaver = container.Resolve<ISaver<ServerMember>>();
            serverSaver = container.Resolve<ISaver<ChatServer>>();
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

        #region GetServerMemberAsync

        [Fact]
        public async Task GetServerMember_ActiveMember_ReturnsMappedEntity()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var server = await CreateServerAsync("server", user.Id, ct);
            var expected = await AddMemberAsync(server.Id, user.Id, BaseDate, ct);

            var result = await repository.GetServerMemberAsync(server.Id, user.Id, ct);

            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expected, options => options
                .Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromMilliseconds(1)))
                .WhenTypeIs<DateTime>());
        }

        [Fact]
        public async Task GetServerMember_NotMember_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var otherUser = await CreateUserAsync("other", ct);
            var server = await CreateServerAsync("server", user.Id, ct);
            await AddMemberAsync(server.Id, user.Id, BaseDate, ct);

            var result = await repository.GetServerMemberAsync(server.Id, otherUser.Id, ct);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetServerMember_MemberOfAnotherServer_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var server = await CreateServerAsync("server", user.Id, ct);
            var otherServer = await CreateServerAsync("other", user.Id, ct);
            await AddMemberAsync(otherServer.Id, user.Id, BaseDate, ct);

            var result = await repository.GetServerMemberAsync(server.Id, user.Id, ct);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetServerMember_DeletedMembership_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var server = await CreateServerAsync("server", user.Id, ct);
            var member = await AddMemberAsync(server.Id, user.Id, BaseDate, ct);
            await MarkMemberAsDeletedAsync(member.Id, ct);

            var result = await repository.GetServerMemberAsync(server.Id, user.Id, ct);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetServerMember_RejoinedAfterLeaving_ReturnsActiveMembership()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var server = await CreateServerAsync("server", user.Id, ct);
            var previous = await AddMemberAsync(server.Id, user.Id, BaseDate, ct);
            await MarkMemberAsDeletedAsync(previous.Id, ct);
            var current = await AddMemberAsync(server.Id, user.Id, BaseDate.AddDays(1), ct);

            var result = await repository.GetServerMemberAsync(server.Id, user.Id, ct);

            result.Should().NotBeNull();
            result.Id.Should().Be(current.Id);
        }

        [Fact]
        public async Task GetServerMember_JoinedThroughInvitation_ReturnsInvitationId()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner", ct);
            var user = await CreateUserAsync("user", ct);
            var server = await CreateServerAsync("server", owner.Id, ct);
            var invitationId = await CreateAcceptedInvitationAsync(server.Id, owner.Id, user.Id, ct);
            var expected = await AddMemberAsync(server.Id, user.Id, BaseDate, ct, invitationId);

            var result = await repository.GetServerMemberAsync(server.Id, user.Id, ct);

            result.Should().NotBeNull();
            result.Id.Should().Be(expected.Id);
            result.InvitationId.Should().Be(invitationId);
        }

        #endregion

        #region GetServerMemberDtosAsync - filtering

        [Fact]
        public async Task GetServerMemberDtos_ServerWithoutMembers_ReturnsEmptyPage()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner", ct);
            var server = await CreateServerAsync("server", owner.Id, ct);

            var result = await repository.GetServerMemberDtosAsync(server.Id, new PageRequest(), ct);

            result.Items.Should().BeEmpty();
            result.Next.Should().BeNull();
            result.Total.Should().Be(0);
        }

        [Fact]
        public async Task GetServerMemberDtos_ReturnsMappedDto()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var server = await CreateServerAsync("server", user.Id, ct);
            var member = await AddMemberAsync(server.Id, user.Id, BaseDate, ct);

            var result = await repository.GetServerMemberDtosAsync(server.Id, new PageRequest(), ct);

            result.Items.Should().ContainSingle();
            var dto = result.Items[0];
            dto.Id.Should().Be(member.Id);
            dto.UserId.Should().Be(user.Id);
            dto.Username.Should().Be("user");
            dto.CreatedAt.Should().BeCloseTo(member.CreatedAt, TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public async Task GetServerMemberDtos_ReturnsOnlyMembersOfRequestedServer()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerWithMembersAsync(["a", "b"], ct);
            var outsider = await CreateUserAsync("outsider", ct);
            var otherServer = await CreateServerAsync("other", outsider.Id, ct);
            await AddMemberAsync(otherServer.Id, outsider.Id, BaseDate, ct);
            await AddMemberAsync(otherServer.Id, members[0].UserId, BaseDate, ct);

            var result = await repository.GetServerMemberDtosAsync(server.Id, new PageRequest(), ct);

            result.Items.Select(m => m.Id).Should().BeEquivalentTo(members.Select(m => m.Id));
            result.Total.Should().Be(2);
        }

        [Fact]
        public async Task GetServerMemberDtos_DeletedMember_IsExcludedFromItemsAndTotal()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerWithMembersAsync(["kept", "left"], ct);
            await MarkMemberAsDeletedAsync(members[1].Id, ct);

            var result = await repository.GetServerMemberDtosAsync(server.Id, new PageRequest(), ct);

            result.Items.Select(m => m.Id).Should().Equal(members[0].Id);
            result.Total.Should().Be(1);
        }

        [Fact]
        public async Task GetServerMemberDtos_RejoinedMember_AppearsOnceWithActiveMembership()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerWithMembersAsync(["user"], ct);
            await MarkMemberAsDeletedAsync(members[0].Id, ct);
            var current = await AddMemberAsync(server.Id, members[0].UserId, BaseDate.AddDays(1), ct);

            var result = await repository.GetServerMemberDtosAsync(server.Id, new PageRequest(), ct);

            result.Items.Select(m => m.Id).Should().Equal(current.Id);
            result.Total.Should().Be(1);
        }

        #endregion

        #region GetServerMemberDtosAsync - sorting

        [Fact]
        public async Task GetServerMemberDtos_DefaultRequest_SortsByCreatedAtDescending()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerWithMembersAsync(["b", "c", "a"], ct);

            var result = await repository.GetServerMemberDtosAsync(server.Id, new PageRequest(), ct);

            // Members join one minute apart, in the given order
            result.Items.Select(m => m.Id).Should().Equal(members[2].Id, members[1].Id, members[0].Id);
        }

        [Fact]
        public async Task GetServerMemberDtos_CreatedAtAscending_SortsOldestFirst()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerWithMembersAsync(["b", "c", "a"], ct);

            var request = new PageRequest { SortBy = "createdAt", Descending = false };
            var result = await repository.GetServerMemberDtosAsync(server.Id, request, ct);

            result.Items.Select(m => m.Id).Should().Equal(members[0].Id, members[1].Id, members[2].Id);
        }

        [Theory]
        [InlineData("username", false)]
        [InlineData("UserName", false)]
        [InlineData("username", true)]
        public async Task GetServerMemberDtos_SortByUsername_SortsIgnoringCaseAndAccents(string sortBy, bool descending)
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, _) = await CreateServerWithMembersAsync(["Zeta", "beta", "Álvaro", "alfa"], ct);

            var request = new PageRequest { SortBy = sortBy, Descending = descending };
            var result = await repository.GetServerMemberDtosAsync(server.Id, request, ct);

            var expected = new[] { "alfa", "Álvaro", "beta", "Zeta" };
            result.Items.Select(m => m.Username).Should().Equal(descending ? Enumerable.Reverse(expected) : expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("unknown")]
        [InlineData("status")]
        public async Task GetServerMemberDtos_UnknownSortBy_FallsBackToCreatedAt(string? sortBy)
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerWithMembersAsync(["b", "a"], ct);

            var request = new PageRequest { SortBy = sortBy, Descending = true };
            var result = await repository.GetServerMemberDtosAsync(server.Id, request, ct);

            result.Items.Select(m => m.Id).Should().Equal(members[1].Id, members[0].Id);
        }

        #endregion

        #region GetServerMemberDtosAsync - pagination

        [Fact]
        public async Task GetServerMemberDtos_FirstPage_ReturnsPageSizeItemsAndNextCursor()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, _) = await CreateServerWithMembersAsync(["a", "b", "c", "d", "e"], ct);

            var request = new PageRequest { PageSize = 2 };
            var result = await repository.GetServerMemberDtosAsync(server.Id, request, ct);

            result.Items.Should().HaveCount(2);
            result.Next.Should().Be(result.Items[^1].Id);
            result.PageSize.Should().Be(2);
            result.Total.Should().Be(5);
        }

        [Fact]
        public async Task GetServerMemberDtos_ExactlyPageSizeItems_ReturnsNoNextCursor()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, _) = await CreateServerWithMembersAsync(["a", "b"], ct);

            var request = new PageRequest { PageSize = 2 };
            var result = await repository.GetServerMemberDtosAsync(server.Id, request, ct);

            result.Items.Should().HaveCount(2);
            result.Next.Should().BeNull();
        }

        [Theory]
        [InlineData("createdat", true)]
        [InlineData("createdat", false)]
        [InlineData("username", true)]
        [InlineData("username", false)]
        public async Task GetServerMemberDtos_WalkingAllPages_ReturnsEveryMemberOnceInOrder(string sortBy, bool descending)
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, _) = await CreateServerWithMembersAsync(["u7", "u6", "u5", "u4", "u3", "u2", "u1"], ct);

            var all = await repository.GetServerMemberDtosAsync(server.Id, new PageRequest { SortBy = sortBy, Descending = descending, PageSize = 50 }, ct);
            var pages = await WalkAllPagesAsync(server.Id, new PageRequest { SortBy = sortBy, Descending = descending, PageSize = 3 }, ct);

            pages.Select(p => p.Items.Count).Should().Equal(3, 3, 1);
            pages.Should().OnlyContain(p => p.Total == 7);
            var walked = pages.SelectMany(p => p.Items).Select(m => m.Id).ToList();
            walked.Should().OnlyHaveUniqueItems();
            walked.Should().Equal(all.Items.Select(m => m.Id));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetServerMemberDtos_TiedCreatedAt_UsesIdAsTieBreakerWithoutLosingRows(bool descending)
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner", ct);
            var server = await CreateServerAsync("server", owner.Id, ct);
            var created = new List<Guid>();
            for (var i = 0; i < 5; i++)
            {
                var user = await CreateUserAsync($"user{i}", ct);
                // Same CreatedAt for every member
                created.Add((await AddMemberAsync(server.Id, user.Id, BaseDate, ct)).Id);
            }

            var all = await repository.GetServerMemberDtosAsync(server.Id, new PageRequest { Descending = descending, PageSize = 50 }, ct);
            var pages = await WalkAllPagesAsync(server.Id, new PageRequest { Descending = descending, PageSize = 2 }, ct);

            var walked = pages.SelectMany(p => p.Items).Select(m => m.Id).ToList();
            walked.Should().OnlyHaveUniqueItems();
            walked.Should().BeEquivalentTo(created);
            walked.Should().Equal(all.Items.Select(m => m.Id));
        }

        [Fact]
        public async Task GetServerMemberDtos_WalkingPagesWithDeletedMembers_SkipsThem()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerWithMembersAsync(["a", "b", "c", "d", "e"], ct);
            await MarkMemberAsDeletedAsync(members[1].Id, ct);
            await MarkMemberAsDeletedAsync(members[3].Id, ct);

            var pages = await WalkAllPagesAsync(server.Id, new PageRequest { PageSize = 2 }, ct);

            pages.Should().OnlyContain(p => p.Total == 3);
            pages.SelectMany(p => p.Items).Select(m => m.Id).Should().Equal(members[4].Id, members[2].Id, members[0].Id);
        }

        [Fact]
        public async Task GetServerMemberDtos_CursorFromAnotherServer_IsUsedAsPositionWithoutLeakingRows()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerWithMembersAsync(["a", "b", "c"], ct);
            var outsider = await CreateUserAsync("outsider", ct);
            var otherServer = await CreateServerAsync("other", outsider.Id, ct);
            // Joined between "b" (minute 1) and "c" (minute 2)
            var foreign = await AddMemberAsync(otherServer.Id, outsider.Id, BaseDate.AddMinutes(1).AddSeconds(30), ct);

            var request = new PageRequest { Next = foreign.Id };
            var result = await repository.GetServerMemberDtosAsync(server.Id, request, ct);

            // The cursor subquery is not filtered by server, so the foreign row only sets the position
            result.Items.Select(m => m.Id).Should().Equal(members[1].Id, members[0].Id);
            result.Items.Should().NotContain(m => m.Id == foreign.Id);
            result.Total.Should().Be(3);
        }

        #endregion

        #region Cancellation

        [Theory]
        [InlineData(nameof(IServerMemberRepository.GetServerMemberAsync))]
        [InlineData(nameof(IServerMemberRepository.GetServerMemberDtosAsync))]
        public async Task AnyMethod_CancelledToken_ThrowsOperationCanceled(string method)
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, members) = await CreateServerWithMembersAsync(["user"], ct);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            Func<Task> act = method switch
            {
                nameof(IServerMemberRepository.GetServerMemberAsync) => () => repository.GetServerMemberAsync(server.Id, members[0].UserId, cts.Token),
                nameof(IServerMemberRepository.GetServerMemberDtosAsync) => () => repository.GetServerMemberDtosAsync(server.Id, new PageRequest(), cts.Token),
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

        private async Task<ChatServer> CreateServerAsync(string name, Guid ownerId, CancellationToken ct)
        {
            var server = Persistable.CreateNew<ChatServer>();
            server.Name = name;
            server.Description = $"Description of {name}";
            server.OwnerId = ownerId;
            await serverSaver.SaveAsync(server, ct);
            return server;
        }

        private async Task<ServerMember> AddMemberAsync(Guid serverId, Guid userId, DateTime createdAt, CancellationToken ct, Guid? invitationId = null)
        {
            var member = Persistable.CreateNew<ServerMember>();
            member.ChatServerId = serverId;
            member.UserId = userId;
            member.InvitationId = invitationId;
            member.CreatedAt = createdAt;
            member.ModifiedAt = createdAt;
            await memberSaver.SaveAsync(member, ct);
            return member;
        }

        /// <summary>
        /// Creates one server and one user per username, joining one minute apart in the given order.
        /// </summary>
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

        private async Task<Guid> CreateAcceptedInvitationAsync(Guid serverId, Guid invitedBy, Guid userId, CancellationToken ct)
        {
            var invitationId = Guid.CreateVersion7();
            await using var conn = await provider.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                """
                    INSERT INTO "HootOut"."ServerInvitation" ("Id", "ChatServerId", "InvitedBy", "UserId", "Status", "CreatedAt", "ModifiedAt")
                    VALUES (@Id, @ServerId, @InvitedBy, @UserId, 10, @Now, @Now)
                """,
                new { Id = invitationId, ServerId = serverId, InvitedBy = invitedBy, UserId = userId, Now = BaseDate },
                cancellationToken: ct));
            return invitationId;
        }

        private async Task MarkMemberAsDeletedAsync(Guid memberId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                """UPDATE "HootOut"."ServerMember" SET "Deleted" = true WHERE "Id" = @Id""",
                new { Id = memberId },
                cancellationToken: ct));
        }

        private async Task<List<PageResult<ServerMemberDto>>> WalkAllPagesAsync(Guid serverId, PageRequest firstRequest, CancellationToken ct)
        {
            var pages = new List<PageResult<ServerMemberDto>>();
            var request = firstRequest;

            // Upper bound to avoid an infinite loop if the cursor never ends
            for (var i = 0; i < 20; i++)
            {
                var page = await repository.GetServerMemberDtosAsync(serverId, request, ct);
                pages.Add(page);

                if (page.Next is null)
                    return pages;

                request = request with { Next = page.Next };
            }

            throw new InvalidOperationException("Pagination did not finish after 20 pages");
        }
    }
}
