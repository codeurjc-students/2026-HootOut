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
using HootOut.Contracts.ChatServers.Enums;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Saver;
using HootOut.Infraestructure.DI;
using HootOut.Users.Entities;

namespace HootOut.PostgreSQL.IntegrationTests.Repositories.ChatServers
{
    [Collection("IntegrationTests")]
    public class ServerInvitationRepositoryTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private static readonly DateTime BaseDate = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private readonly IContainer container;

        private readonly ClearAllTables clearTables;

        private readonly IServerInvitationRepository repository;

        private readonly ISaver<ServerInvitation> invitationSaver;

        private readonly ISaver<ServerMember> memberSaver;

        private readonly ISaver<ChatServer> serverSaver;

        private readonly ISaver<UserInfo> userSaver;

        private readonly IPersistenceProvider provider;

        public ServerInvitationRepositoryTests(PostgresTestContainer postgresContainer)
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

            repository = container.Resolve<IServerInvitationRepository>();
            invitationSaver = container.Resolve<ISaver<ServerInvitation>>();
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

        #region GetServerInvitationDtosAsync

        [Fact]
        public async Task GetServerInvitationDtos_ServerWithoutInvitations_ReturnsEmptyPage()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner", ct);
            var server = await CreateServerAsync("server", owner.Id, ct);

            var result = await repository.GetServerInvitationDtosAsync(server.Id, new PageRequest(), ct);

            result.Items.Should().BeEmpty();
            result.Next.Should().BeNull();
            result.Total.Should().Be(0);
        }

        [Fact]
        public async Task GetServerInvitationDtos_ReturnsMappedDtoWithInviteeUsername()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner", ct);
            var invitee = await CreateUserAsync("invitee", ct);
            var server = await CreateServerAsync("server", owner.Id, ct);
            var invitation = await CreateInvitationAsync(server.Id, owner.Id, invitee.Id, ServerInvitationStatus.Pending, BaseDate, ct);

            var result = await repository.GetServerInvitationDtosAsync(server.Id, new PageRequest(), ct);

            result.Items.Should().ContainSingle();
            var dto = result.Items[0];
            dto.Id.Should().Be(invitation.Id);
            dto.ChatServerId.Should().Be(server.Id);
            dto.ServerName.Should().Be("server");
            dto.UserId.Should().Be(invitee.Id);
            // In the server listing, Username is the invited user
            dto.Username.Should().Be("invitee");
            dto.Status.Should().Be(ServerInvitationStatus.Pending);
            dto.CreatedAt.Should().BeCloseTo(invitation.CreatedAt, TimeSpan.FromMilliseconds(1));
            dto.ModifiedAt.Should().BeCloseTo(invitation.ModifiedAt, TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public async Task GetServerInvitationDtos_ReturnsEveryStatusOfRequestedServerOnly()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner", ct);
            var server = await CreateServerAsync("server", owner.Id, ct);
            var otherServer = await CreateServerAsync("other", owner.Id, ct);
            var expected = new List<Guid>();
            var statuses = Enum.GetValues<ServerInvitationStatus>();
            for (var i = 0; i < statuses.Length; i++)
            {
                var invitee = await CreateUserAsync($"invitee{i}", ct);
                var invitation = await CreateInvitationAsync(server.Id, owner.Id, invitee.Id, statuses[i], BaseDate.AddMinutes(i), ct);
                expected.Add(invitation.Id);
                await CreateInvitationAsync(otherServer.Id, owner.Id, invitee.Id, ServerInvitationStatus.Pending, BaseDate, ct);
            }

            var result = await repository.GetServerInvitationDtosAsync(server.Id, new PageRequest(), ct);

            result.Items.Select(i => i.Id).Should().BeEquivalentTo(expected);
            result.Items.Select(i => i.Status).Should().BeEquivalentTo(statuses);
            result.Items.Should().OnlyContain(i => i.ChatServerId == server.Id);
            result.Total.Should().Be(statuses.Length);
        }

        [Fact]
        public async Task GetServerInvitationDtos_DefaultRequest_SortsByCreatedAtDescending()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, invitations) = await CreateServerWithInviteesAsync(["b", "c", "a"], ct);

            var result = await repository.GetServerInvitationDtosAsync(server.Id, new PageRequest(), ct);

            // Invitations are created one minute apart, in the given order
            result.Items.Select(i => i.Id).Should().Equal(invitations[2].Id, invitations[1].Id, invitations[0].Id);
        }

        [Fact]
        public async Task GetServerInvitationDtos_CreatedAtAscending_SortsOldestFirst()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, invitations) = await CreateServerWithInviteesAsync(["b", "c", "a"], ct);

            var request = new PageRequest { SortBy = "createdAt", Descending = false };
            var result = await repository.GetServerInvitationDtosAsync(server.Id, request, ct);

            result.Items.Select(i => i.Id).Should().Equal(invitations[0].Id, invitations[1].Id, invitations[2].Id);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetServerInvitationDtos_SortByStatus_SortsByStatusValue(bool descending)
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner", ct);
            var server = await CreateServerAsync("server", owner.Id, ct);
            ServerInvitationStatus[] statuses = [ServerInvitationStatus.Rejected, ServerInvitationStatus.Pending, ServerInvitationStatus.Revoked, ServerInvitationStatus.Accepted];
            for (var i = 0; i < statuses.Length; i++)
            {
                var invitee = await CreateUserAsync($"invitee{i}", ct);
                await CreateInvitationAsync(server.Id, owner.Id, invitee.Id, statuses[i], BaseDate.AddMinutes(i), ct);
            }

            var request = new PageRequest { SortBy = "status", Descending = descending };
            var result = await repository.GetServerInvitationDtosAsync(server.Id, request, ct);

            var expected = new[] { ServerInvitationStatus.Pending, ServerInvitationStatus.Accepted, ServerInvitationStatus.Rejected, ServerInvitationStatus.Revoked };
            result.Items.Select(i => i.Status).Should().Equal(descending ? Enumerable.Reverse(expected) : expected);
        }

        [Theory]
        [InlineData("username", false)]
        [InlineData("USERNAME", false)]
        [InlineData("username", true)]
        public async Task GetServerInvitationDtos_SortByUsername_SortsInviteesIgnoringCaseAndAccents(string sortBy, bool descending)
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, _) = await CreateServerWithInviteesAsync(["Zeta", "beta", "Álvaro", "alfa"], ct);

            var request = new PageRequest { SortBy = sortBy, Descending = descending };
            var result = await repository.GetServerInvitationDtosAsync(server.Id, request, ct);

            var expected = new[] { "alfa", "Álvaro", "beta", "Zeta" };
            result.Items.Select(i => i.Username).Should().Equal(descending ? Enumerable.Reverse(expected) : expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("unknown")]
        [InlineData("servername")]
        public async Task GetServerInvitationDtos_UnknownSortBy_FallsBackToCreatedAt(string? sortBy)
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, invitations) = await CreateServerWithInviteesAsync(["b", "a"], ct);

            var request = new PageRequest { SortBy = sortBy, Descending = true };
            var result = await repository.GetServerInvitationDtosAsync(server.Id, request, ct);

            result.Items.Select(i => i.Id).Should().Equal(invitations[1].Id, invitations[0].Id);
        }

        [Theory]
        [InlineData("createdat", true)]
        [InlineData("createdat", false)]
        [InlineData("status", true)]
        [InlineData("status", false)]
        [InlineData("username", true)]
        [InlineData("username", false)]
        public async Task GetServerInvitationDtos_WalkingAllPages_ReturnsEveryInvitationOnceInOrder(string sortBy, bool descending)
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner", ct);
            var server = await CreateServerAsync("server", owner.Id, ct);
            var statuses = Enum.GetValues<ServerInvitationStatus>();
            for (var i = 0; i < 7; i++)
            {
                var invitee = await CreateUserAsync($"invitee{7 - i:D2}", ct);
                // Repeated statuses force the Id tie breaker
                await CreateInvitationAsync(server.Id, owner.Id, invitee.Id, statuses[i % statuses.Length], BaseDate.AddMinutes(i), ct);
            }

            var all = await repository.GetServerInvitationDtosAsync(server.Id, new PageRequest { SortBy = sortBy, Descending = descending, PageSize = 50 }, ct);
            var pages = await WalkAllPagesAsync(r => repository.GetServerInvitationDtosAsync(server.Id, r, ct), new PageRequest { SortBy = sortBy, Descending = descending, PageSize = 3 });

            pages.Select(p => p.Items.Count).Should().Equal(3, 3, 1);
            pages.Should().OnlyContain(p => p.Total == 7);
            var walked = pages.SelectMany(p => p.Items).Select(i => i.Id).ToList();
            walked.Should().OnlyHaveUniqueItems();
            walked.Should().Equal(all.Items.Select(i => i.Id));
        }

        [Fact]
        public async Task GetServerInvitationDtos_CursorFromOtherServer_ReturnsEmptyItems()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, _) = await CreateServerWithInviteesAsync(["a", "b"], ct);
            var (_, otherInvitations) = await CreateServerWithInviteesAsync(["c"], ct, ownerName: "owner2", serverName: "other");

            var request = new PageRequest { Next = otherInvitations[0].Id };
            var result = await repository.GetServerInvitationDtosAsync(server.Id, request, ct);

            result.Items.Should().BeEmpty();
            result.Total.Should().Be(2);
        }

        #endregion

        #region GetUserServerInvitationDtosAsync

        [Fact]
        public async Task GetUserServerInvitationDtos_UserWithoutInvitations_ReturnsEmptyPage()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);

            var result = await repository.GetUserServerInvitationDtosAsync(user.Id, new PageRequest(), ct);

            result.Items.Should().BeEmpty();
            result.Next.Should().BeNull();
            result.Total.Should().Be(0);
        }

        [Fact]
        public async Task GetUserServerInvitationDtos_ReturnsMappedDtoWithInviterUsername()
        {
            var ct = TestContext.Current.CancellationToken;
            var inviter = await CreateUserAsync("inviter", ct);
            var user = await CreateUserAsync("user", ct);
            var server = await CreateServerAsync("server", inviter.Id, ct);
            var invitation = await CreateInvitationAsync(server.Id, inviter.Id, user.Id, ServerInvitationStatus.Pending, BaseDate, ct);

            var result = await repository.GetUserServerInvitationDtosAsync(user.Id, new PageRequest(), ct);

            result.Items.Should().ContainSingle();
            var dto = result.Items[0];
            dto.Id.Should().Be(invitation.Id);
            dto.ChatServerId.Should().Be(server.Id);
            dto.ServerName.Should().Be("server");
            dto.UserId.Should().Be(user.Id);
            // In the user listing, Username is the user who sent the invitation
            dto.Username.Should().Be("inviter");
            dto.Status.Should().Be(ServerInvitationStatus.Pending);
            dto.CreatedAt.Should().BeCloseTo(invitation.CreatedAt, TimeSpan.FromMilliseconds(1));
            dto.ModifiedAt.Should().BeCloseTo(invitation.ModifiedAt, TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public async Task GetUserServerInvitationDtos_ReturnsOnlyPendingInvitationsOfUser()
        {
            var ct = TestContext.Current.CancellationToken;
            var inviter = await CreateUserAsync("inviter", ct);
            var user = await CreateUserAsync("user", ct);
            var otherUser = await CreateUserAsync("other", ct);
            var pendingServer = await CreateServerAsync("pending", inviter.Id, ct);
            var pending = await CreateInvitationAsync(pendingServer.Id, inviter.Id, user.Id, ServerInvitationStatus.Pending, BaseDate, ct);
            foreach (var status in new[] { ServerInvitationStatus.Accepted, ServerInvitationStatus.Rejected, ServerInvitationStatus.Revoked })
            {
                var server = await CreateServerAsync(status.ToString(), inviter.Id, ct);
                await CreateInvitationAsync(server.Id, inviter.Id, user.Id, status, BaseDate, ct);
            }
            await CreateInvitationAsync(pendingServer.Id, inviter.Id, otherUser.Id, ServerInvitationStatus.Pending, BaseDate, ct);

            var result = await repository.GetUserServerInvitationDtosAsync(user.Id, new PageRequest(), ct);

            result.Items.Select(i => i.Id).Should().Equal(pending.Id);
            result.Total.Should().Be(1);
        }

        [Fact]
        public async Task GetUserServerInvitationDtos_DefaultRequest_SortsByCreatedAtDescending()
        {
            var ct = TestContext.Current.CancellationToken;
            var (user, invitations) = await CreateUserWithInvitationsAsync(["b", "c", "a"], ["x", "y", "z"], ct);

            var result = await repository.GetUserServerInvitationDtosAsync(user.Id, new PageRequest(), ct);

            result.Items.Select(i => i.Id).Should().Equal(invitations[2].Id, invitations[1].Id, invitations[0].Id);
        }

        [Theory]
        [InlineData("servername", false)]
        [InlineData("ServerName", false)]
        [InlineData("servername", true)]
        public async Task GetUserServerInvitationDtos_SortByServerName_SortsIgnoringCaseAndAccents(string sortBy, bool descending)
        {
            var ct = TestContext.Current.CancellationToken;
            var (user, _) = await CreateUserWithInvitationsAsync(["Zeta", "beta", "Álvaro", "alfa"], ["w", "x", "y", "z"], ct);

            var request = new PageRequest { SortBy = sortBy, Descending = descending };
            var result = await repository.GetUserServerInvitationDtosAsync(user.Id, request, ct);

            var expected = new[] { "alfa", "Álvaro", "beta", "Zeta" };
            result.Items.Select(i => i.ServerName).Should().Equal(descending ? Enumerable.Reverse(expected) : expected);
        }

        [Theory]
        [InlineData("invitedby", false)]
        [InlineData("invitedBy", false)]
        [InlineData("invitedby", true)]
        public async Task GetUserServerInvitationDtos_SortByInvitedBy_SortsInvitersIgnoringCaseAndAccents(string sortBy, bool descending)
        {
            var ct = TestContext.Current.CancellationToken;
            var (user, _) = await CreateUserWithInvitationsAsync(["s1", "s2", "s3", "s4"], ["Zeta", "beta", "Álvaro", "alfa"], ct);

            var request = new PageRequest { SortBy = sortBy, Descending = descending };
            var result = await repository.GetUserServerInvitationDtosAsync(user.Id, request, ct);

            var expected = new[] { "alfa", "Álvaro", "beta", "Zeta" };
            result.Items.Select(i => i.Username).Should().Equal(descending ? Enumerable.Reverse(expected) : expected);
        }

        [Theory]
        [InlineData("createdat", true)]
        [InlineData("createdat", false)]
        [InlineData("status", true)]
        [InlineData("servername", false)]
        [InlineData("invitedby", true)]
        public async Task GetUserServerInvitationDtos_WalkingAllPages_ReturnsEveryInvitationOnceInOrder(string sortBy, bool descending)
        {
            var ct = TestContext.Current.CancellationToken;
            string[] serverNames = ["s7", "s6", "s5", "s4", "s3", "s2", "s1"];
            // Repeated inviters force the Id tie breaker
            string[] inviters = ["i1", "i2", "i1", "i2", "i1", "i2", "i1"];
            var (user, _) = await CreateUserWithInvitationsAsync(serverNames, inviters, ct);

            var all = await repository.GetUserServerInvitationDtosAsync(user.Id, new PageRequest { SortBy = sortBy, Descending = descending, PageSize = 50 }, ct);
            var pages = await WalkAllPagesAsync(r => repository.GetUserServerInvitationDtosAsync(user.Id, r, ct), new PageRequest { SortBy = sortBy, Descending = descending, PageSize = 3 });

            pages.Select(p => p.Items.Count).Should().Equal(3, 3, 1);
            pages.Should().OnlyContain(p => p.Total == 7);
            var walked = pages.SelectMany(p => p.Items).Select(i => i.Id).ToList();
            walked.Should().OnlyHaveUniqueItems();
            walked.Should().Equal(all.Items.Select(i => i.Id));
        }

        #endregion

        #region GetServerInvitationByIdAsync

        [Theory]
        [InlineData(ServerInvitationStatus.Pending)]
        [InlineData(ServerInvitationStatus.Accepted)]
        [InlineData(ServerInvitationStatus.Rejected)]
        [InlineData(ServerInvitationStatus.Revoked)]
        public async Task GetServerInvitationById_Existing_ReturnsMappedEntityInAnyStatus(ServerInvitationStatus status)
        {
            var ct = TestContext.Current.CancellationToken;
            var inviter = await CreateUserAsync("inviter", ct);
            var user = await CreateUserAsync("user", ct);
            var server = await CreateServerAsync("server", inviter.Id, ct);
            var expected = await CreateInvitationAsync(server.Id, inviter.Id, user.Id, status, BaseDate, ct);

            var result = await repository.GetServerInvitationByIdAsync(expected.Id, ct);

            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expected, options => options
                .Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromMilliseconds(1)))
                .WhenTypeIs<DateTime>());
        }

        [Fact]
        public async Task GetServerInvitationById_NotExisting_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            await CreateServerWithInviteesAsync(["a"], ct);

            var result = await repository.GetServerInvitationByIdAsync(Guid.NewGuid(), ct);

            result.Should().BeNull();
        }

        #endregion

        #region AcceptServerInvitationAsync

        [Fact]
        public async Task AcceptServerInvitation_Pending_CreatesMemberAndMarksInvitationAsAccepted()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, invitation) = await CreatePendingInvitationAsync(ct);

            var before = DateTime.UtcNow;
            var memberId = await repository.AcceptServerInvitationAsync(invitation, ct);

            memberId.Should().NotBeNull();
            var members = await GetMembersAsync(server.Id, invitation.UserId, ct);
            members.Should().ContainSingle();
            members[0].Id.Should().Be(memberId!.Value);
            members[0].InvitationId.Should().Be(invitation.Id);
            members[0].Deleted.Should().BeFalse();
            members[0].CreatedAt.Should().BeOnOrAfter(before.AddMilliseconds(-1));

            var stored = await repository.GetServerInvitationByIdAsync(invitation.Id, ct);
            stored!.Status.Should().Be(ServerInvitationStatus.Accepted);
            stored.ModifiedAt.Should().BeOnOrAfter(before.AddMilliseconds(-1));
            stored.ModifiedAt.Should().BeCloseTo(members[0].CreatedAt, TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public async Task AcceptServerInvitation_Pending_RemovesItFromUserPendingInvitations()
        {
            var ct = TestContext.Current.CancellationToken;
            var (_, invitation) = await CreatePendingInvitationAsync(ct);

            await repository.AcceptServerInvitationAsync(invitation, ct);

            var result = await repository.GetUserServerInvitationDtosAsync(invitation.UserId, new PageRequest(), ct);
            result.Items.Should().BeEmpty();
        }

        [Theory]
        [InlineData(ServerInvitationStatus.Accepted)]
        [InlineData(ServerInvitationStatus.Rejected)]
        [InlineData(ServerInvitationStatus.Revoked)]
        public async Task AcceptServerInvitation_NotPending_ReturnsNullAndChangesNothing(ServerInvitationStatus status)
        {
            var ct = TestContext.Current.CancellationToken;
            var inviter = await CreateUserAsync("inviter", ct);
            var user = await CreateUserAsync("user", ct);
            var server = await CreateServerAsync("server", inviter.Id, ct);
            var invitation = await CreateInvitationAsync(server.Id, inviter.Id, user.Id, status, BaseDate, ct);

            var memberId = await repository.AcceptServerInvitationAsync(invitation, ct);

            memberId.Should().BeNull();
            (await GetMembersAsync(server.Id, user.Id, ct)).Should().BeEmpty();
            var stored = await repository.GetServerInvitationByIdAsync(invitation.Id, ct);
            stored!.Status.Should().Be(status);
            stored.ModifiedAt.Should().BeCloseTo(BaseDate, TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public async Task AcceptServerInvitation_DifferentUser_ReturnsNullAndChangesNothing()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, invitation) = await CreatePendingInvitationAsync(ct);
            var intruder = await CreateUserAsync("intruder", ct);
            var forged = new ServerInvitation
            {
                Id = invitation.Id,
                ChatServerId = invitation.ChatServerId,
                InvitedBy = invitation.InvitedBy,
                UserId = intruder.Id,
                Status = invitation.Status
            };

            var memberId = await repository.AcceptServerInvitationAsync(forged, ct);

            memberId.Should().BeNull();
            (await GetMembersAsync(server.Id, intruder.Id, ct)).Should().BeEmpty();
            (await GetMembersAsync(server.Id, invitation.UserId, ct)).Should().BeEmpty();
            (await repository.GetServerInvitationByIdAsync(invitation.Id, ct))!.Status.Should().Be(ServerInvitationStatus.Pending);
        }

        [Fact]
        public async Task AcceptServerInvitation_NotExisting_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, invitation) = await CreatePendingInvitationAsync(ct);
            var missing = new ServerInvitation
            {
                Id = Guid.NewGuid(),
                ChatServerId = server.Id,
                InvitedBy = invitation.InvitedBy,
                UserId = invitation.UserId
            };

            var memberId = await repository.AcceptServerInvitationAsync(missing, ct);

            memberId.Should().BeNull();
            (await GetMembersAsync(server.Id, invitation.UserId, ct)).Should().BeEmpty();
        }

        [Fact]
        public async Task AcceptServerInvitation_Twice_SecondReturnsNullAndKeepsSingleMember()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, invitation) = await CreatePendingInvitationAsync(ct);

            var first = await repository.AcceptServerInvitationAsync(invitation, ct);
            var second = await repository.AcceptServerInvitationAsync(invitation, ct);

            first.Should().NotBeNull();
            second.Should().BeNull();
            var members = await GetMembersAsync(server.Id, invitation.UserId, ct);
            members.Select(m => m.Id).Should().Equal(first!.Value);
        }

        [Fact]
        public async Task AcceptServerInvitation_Concurrently_OnlyOneSucceeds()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, invitation) = await CreatePendingInvitationAsync(ct);

            var results = await Task.WhenAll(Enumerable.Range(0, 5)
                .Select(_ => repository.AcceptServerInvitationAsync(invitation, ct)));

            results.Count(r => r is not null).Should().Be(1);
            (await GetMembersAsync(server.Id, invitation.UserId, ct)).Should().ContainSingle();
        }

        [Fact]
        public async Task AcceptServerInvitation_UserAlreadyActiveMember_ReturnsNullAndKeepsInvitationPending()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, invitation) = await CreatePendingInvitationAsync(ct);
            var existing = await AddMemberAsync(server.Id, invitation.UserId, ct);

            var memberId = await repository.AcceptServerInvitationAsync(invitation, ct);

            memberId.Should().BeNull();
            (await GetMembersAsync(server.Id, invitation.UserId, ct)).Select(m => m.Id).Should().Equal(existing.Id);
            (await repository.GetServerInvitationByIdAsync(invitation.Id, ct))!.Status.Should().Be(ServerInvitationStatus.Pending);
        }

        [Fact]
        public async Task AcceptServerInvitation_UserLeftServerBefore_CreatesNewMember()
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, invitation) = await CreatePendingInvitationAsync(ct);
            var previous = await AddMemberAsync(server.Id, invitation.UserId, ct);
            await MarkMemberAsDeletedAsync(previous.Id, ct);

            var memberId = await repository.AcceptServerInvitationAsync(invitation, ct);

            memberId.Should().NotBeNull();
            var members = await GetMembersAsync(server.Id, invitation.UserId, ct);
            members.Should().HaveCount(2);
            members.Single(m => m.Id == previous.Id).Deleted.Should().BeTrue();
            members.Single(m => m.Id == memberId).Deleted.Should().BeFalse();
            (await repository.GetServerInvitationByIdAsync(invitation.Id, ct))!.Status.Should().Be(ServerInvitationStatus.Accepted);
        }

        #endregion

        #region Cancellation

        [Theory]
        [InlineData(nameof(IServerInvitationRepository.GetServerInvitationDtosAsync))]
        [InlineData(nameof(IServerInvitationRepository.GetUserServerInvitationDtosAsync))]
        [InlineData(nameof(IServerInvitationRepository.GetServerInvitationByIdAsync))]
        [InlineData(nameof(IServerInvitationRepository.AcceptServerInvitationAsync))]
        public async Task AnyMethod_CancelledToken_ThrowsOperationCanceled(string method)
        {
            var ct = TestContext.Current.CancellationToken;
            var (server, invitation) = await CreatePendingInvitationAsync(ct);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            Func<Task> act = method switch
            {
                nameof(IServerInvitationRepository.GetServerInvitationDtosAsync) => () => repository.GetServerInvitationDtosAsync(server.Id, new PageRequest(), cts.Token),
                nameof(IServerInvitationRepository.GetUserServerInvitationDtosAsync) => () => repository.GetUserServerInvitationDtosAsync(invitation.UserId, new PageRequest(), cts.Token),
                nameof(IServerInvitationRepository.GetServerInvitationByIdAsync) => () => repository.GetServerInvitationByIdAsync(invitation.Id, cts.Token),
                nameof(IServerInvitationRepository.AcceptServerInvitationAsync) => () => repository.AcceptServerInvitationAsync(invitation, cts.Token),
                _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
            };

            await act.Should().ThrowAsync<OperationCanceledException>();
            (await GetMembersAsync(server.Id, invitation.UserId, ct)).Should().BeEmpty();
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

        private async Task<ServerInvitation> CreateInvitationAsync(Guid serverId, Guid invitedBy, Guid userId, ServerInvitationStatus status, DateTime createdAt, CancellationToken ct)
        {
            var invitation = Persistable.CreateNew<ServerInvitation>();
            invitation.ChatServerId = serverId;
            invitation.InvitedBy = invitedBy;
            invitation.UserId = userId;
            invitation.Status = status;
            invitation.CreatedAt = createdAt;
            invitation.ModifiedAt = createdAt;
            await invitationSaver.SaveAsync(invitation, ct);
            return invitation;
        }

        private async Task<(ChatServer Server, ServerInvitation Invitation)> CreatePendingInvitationAsync(CancellationToken ct)
        {
            var inviter = await CreateUserAsync("inviter", ct);
            var user = await CreateUserAsync("user", ct);
            var server = await CreateServerAsync("server", inviter.Id, ct);
            var invitation = await CreateInvitationAsync(server.Id, inviter.Id, user.Id, ServerInvitationStatus.Pending, BaseDate, ct);
            return (server, invitation);
        }

        /// <summary>
        /// Creates one server and one pending invitation per invitee, one minute apart, in the given order.
        /// </summary>
        private async Task<(ChatServer Server, List<ServerInvitation> Invitations)> CreateServerWithInviteesAsync(
            string[] invitees, CancellationToken ct, string ownerName = "owner", string serverName = "server")
        {
            var owner = await CreateUserAsync(ownerName, ct);
            var server = await CreateServerAsync(serverName, owner.Id, ct);
            var invitations = new List<ServerInvitation>();
            for (var i = 0; i < invitees.Length; i++)
            {
                var invitee = await CreateUserAsync(invitees[i], ct);
                invitations.Add(await CreateInvitationAsync(server.Id, owner.Id, invitee.Id, ServerInvitationStatus.Pending, BaseDate.AddMinutes(i), ct));
            }

            return (server, invitations);
        }

        /// <summary>
        /// Creates one user with a pending invitation per server, one minute apart; inviters[i] sends the invitation to serverNames[i].
        /// </summary>
        private async Task<(UserInfo User, List<ServerInvitation> Invitations)> CreateUserWithInvitationsAsync(
            string[] serverNames, string[] inviters, CancellationToken ct)
        {
            var user = await CreateUserAsync("user", ct);
            var createdInviters = new Dictionary<string, UserInfo>();
            var invitations = new List<ServerInvitation>();
            for (var i = 0; i < serverNames.Length; i++)
            {
                if (!createdInviters.TryGetValue(inviters[i], out var inviter))
                {
                    inviter = await CreateUserAsync(inviters[i], ct);
                    createdInviters[inviters[i]] = inviter;
                }

                var server = await CreateServerAsync(serverNames[i], inviter.Id, ct);
                invitations.Add(await CreateInvitationAsync(server.Id, inviter.Id, user.Id, ServerInvitationStatus.Pending, BaseDate.AddMinutes(i), ct));
            }

            return (user, invitations);
        }

        private async Task<ServerMember> AddMemberAsync(Guid serverId, Guid userId, CancellationToken ct)
        {
            var member = Persistable.CreateNew<ServerMember>();
            member.ChatServerId = serverId;
            member.UserId = userId;
            await memberSaver.SaveAsync(member, ct);
            return member;
        }

        private async Task MarkMemberAsDeletedAsync(Guid memberId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                """UPDATE "HootOut"."ServerMember" SET "Deleted" = true WHERE "Id" = @Id""",
                new { Id = memberId },
                cancellationToken: ct));
        }

        private async Task<List<ServerMemberRow>> GetMembersAsync(Guid serverId, Guid userId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return (await conn.QueryAsync<ServerMemberRow>(new CommandDefinition(
                """
                    SELECT "Id", "InvitationId", "Deleted", "CreatedAt"
                    FROM "HootOut"."ServerMember"
                    WHERE "ChatServerId" = @ServerId AND "UserId" = @UserId
                """,
                new { ServerId = serverId, UserId = userId },
                cancellationToken: ct))).ToList();
        }

        private static async Task<List<PageResult<ServerInvitationDto>>> WalkAllPagesAsync(
            Func<PageRequest, Task<PageResult<ServerInvitationDto>>> getPage, PageRequest firstRequest)
        {
            var pages = new List<PageResult<ServerInvitationDto>>();
            var request = firstRequest;

            // Upper bound to avoid an infinite loop if the cursor never ends
            for (var i = 0; i < 20; i++)
            {
                var page = await getPage(request);
                pages.Add(page);

                if (page.Next is null)
                    return pages;

                request = request with { Next = page.Next };
            }

            throw new InvalidOperationException("Pagination did not finish after 20 pages");
        }

        private sealed class ServerMemberRow
        {
            public Guid Id { get; set; }
            public Guid? InvitationId { get; set; }
            public bool Deleted { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}
