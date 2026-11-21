using Autofac;
using Dapper;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.Contracts.ChatConversations.Enums;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Enums;
using HootOut.Contracts.Common.Saver;
using HootOut.Contracts.Friends.Dtos;
using HootOut.Friends.Entities;
using HootOut.Friends.Repositories;
using HootOut.Infraestructure.DI;
using HootOut.Users.Entities;

namespace HootOut.PostgreSQL.IntegrationTests.Repositories.Friends
{
    [Collection("IntegrationTests")]
    public class FriendRequestRepositoryTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private static readonly DateTime BaseDate = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private readonly IContainer container;

        private readonly ClearAllTables clearTables;

        private readonly IFriendRequestRepository repository;

        private readonly ISaver<FriendRequest> requestSaver;

        private readonly ISaver<UserInfo> userSaver;

        private readonly IPersistenceProvider provider;

        public FriendRequestRepositoryTests(PostgresTestContainer postgresContainer)
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

            repository = container.Resolve<IFriendRequestRepository>();
            requestSaver = container.Resolve<ISaver<FriendRequest>>();
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

        #region GetFriendRequestByIdAsync

        [Theory]
        [InlineData(InvitationStatus.Pending)]
        [InlineData(InvitationStatus.Accepted)]
        [InlineData(InvitationStatus.Rejected)]
        [InlineData(InvitationStatus.Revoked)]
        public async Task GetFriendRequestById_Existing_ReturnsMappedEntityInAnyStatus(InvitationStatus status)
        {
            var ct = TestContext.Current.CancellationToken;
            var sender = await CreateUserAsync("sender", ct);
            var receiver = await CreateUserAsync("receiver", ct);
            var expected = await CreateRequestAsync(sender.Id, receiver.Id, status, BaseDate, ct);

            var result = await repository.GetFriendRequestByIdAsync(expected.Id, ct);

            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expected, options => options
                .Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromMilliseconds(1)))
                .WhenTypeIs<DateTime>());
        }

        [Fact]
        public async Task GetFriendRequestById_NotExisting_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var sender = await CreateUserAsync("sender", ct);
            var receiver = await CreateUserAsync("receiver", ct);
            await CreateRequestAsync(sender.Id, receiver.Id, InvitationStatus.Pending, BaseDate, ct);

            var result = await repository.GetFriendRequestByIdAsync(Guid.NewGuid(), ct);

            result.Should().BeNull();
        }

        #endregion

        #region GetReceivedFriendRequestsAsync

        [Fact]
        public async Task GetReceivedFriendRequests_UserWithoutRequests_ReturnsEmptyPage()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);

            var result = await repository.GetReceivedFriendRequestsAsync(new PageRequest(), user.Id, ct);

            result.Items.Should().BeEmpty();
            result.Total.Should().Be(0);
            result.Next.Should().BeNull();
        }

        [Fact]
        public async Task GetReceivedFriendRequests_ReturnsMappedDtoWithSenderData()
        {
            var ct = TestContext.Current.CancellationToken;
            var sender = await CreateUserAsync("sender", ct);
            var user = await CreateUserAsync("user", ct);
            var request = await CreateRequestAsync(sender.Id, user.Id, InvitationStatus.Pending, BaseDate, ct);

            var result = await repository.GetReceivedFriendRequestsAsync(new PageRequest(), user.Id, ct);

            var dto = result.Items.Should().ContainSingle().Subject;
            dto.Id.Should().Be(request.Id);
            dto.UserId.Should().Be(user.Id);
            dto.InvitedById.Should().Be(sender.Id);
            dto.InvitedByUsername.Should().Be("sender");
            // The receiver is the user asking, so their username is not needed
            dto.Username.Should().BeEmpty();
            dto.Status.Should().Be(InvitationStatus.Pending);
            dto.CreatedAt.Should().BeCloseTo(request.CreatedAt, TimeSpan.FromMilliseconds(1));
            dto.ModifiedAt.Should().BeCloseTo(request.ModifiedAt, TimeSpan.FromMilliseconds(1));
            result.Total.Should().Be(1);
        }

        [Fact]
        public async Task GetReceivedFriendRequests_ReturnsOnlyPendingRequestsReceivedByUser()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var pendingSender = await CreateUserAsync("pending", ct);
            var other = await CreateUserAsync("other", ct);
            var pending = await CreateRequestAsync(pendingSender.Id, user.Id, InvitationStatus.Pending, BaseDate, ct);
            foreach (var status in new[] { InvitationStatus.Accepted, InvitationStatus.Rejected, InvitationStatus.Revoked })
            {
                var sender = await CreateUserAsync(status.ToString(), ct);
                await CreateRequestAsync(sender.Id, user.Id, status, BaseDate, ct);
            }
            // Sent by the user, not received
            await CreateRequestAsync(user.Id, other.Id, InvitationStatus.Pending, BaseDate, ct);
            // Received by someone else
            await CreateRequestAsync(pendingSender.Id, other.Id, InvitationStatus.Pending, BaseDate, ct);

            var result = await repository.GetReceivedFriendRequestsAsync(new PageRequest(), user.Id, ct);

            result.Items.Select(r => r.Id).Should().Equal(pending.Id);
            result.Total.Should().Be(1);
        }

        [Fact]
        public async Task GetReceivedFriendRequests_DefaultRequest_SortsByCreatedAtDescending()
        {
            var ct = TestContext.Current.CancellationToken;
            var (user, requests) = await CreateReceivedRequestsAsync(["b", "c", "a"], ct);

            var result = await repository.GetReceivedFriendRequestsAsync(new PageRequest(), user.Id, ct);

            result.Items.Select(r => r.Id).Should().Equal(requests[2].Id, requests[1].Id, requests[0].Id);
        }

        [Fact]
        public async Task GetReceivedFriendRequests_CreatedAtAscending_SortsOldestFirst()
        {
            var ct = TestContext.Current.CancellationToken;
            var (user, requests) = await CreateReceivedRequestsAsync(["b", "c", "a"], ct);

            var request = new PageRequest { SortBy = "createdat", Descending = false };
            var result = await repository.GetReceivedFriendRequestsAsync(request, user.Id, ct);

            result.Items.Select(r => r.Id).Should().Equal(requests[0].Id, requests[1].Id, requests[2].Id);
        }

        [Theory]
        [InlineData("invitedby", false)]
        [InlineData("InvitedBy", false)]
        [InlineData("invitedby", true)]
        public async Task GetReceivedFriendRequests_SortByInvitedBy_SortsSendersIgnoringCaseAndAccents(string sortBy, bool descending)
        {
            var ct = TestContext.Current.CancellationToken;
            var (user, _) = await CreateReceivedRequestsAsync(["Zeta", "beta", "Álvaro", "alfa"], ct);

            var request = new PageRequest { SortBy = sortBy, Descending = descending };
            var result = await repository.GetReceivedFriendRequestsAsync(request, user.Id, ct);

            var expected = new[] { "alfa", "Álvaro", "beta", "Zeta" };
            result.Items.Select(r => r.InvitedByUsername).Should().Equal(descending ? Enumerable.Reverse(expected) : expected);
        }

        [Theory]
        [InlineData("username")]
        [InlineData("unknown")]
        [InlineData("")]
        [InlineData(null)]
        public async Task GetReceivedFriendRequests_UnknownSortBy_FallsBackToCreatedAt(string? sortBy)
        {
            var ct = TestContext.Current.CancellationToken;
            var (user, requests) = await CreateReceivedRequestsAsync(["b", "c", "a"], ct);

            var request = new PageRequest { SortBy = sortBy };
            var result = await repository.GetReceivedFriendRequestsAsync(request, user.Id, ct);

            result.Items.Select(r => r.Id).Should().Equal(requests[2].Id, requests[1].Id, requests[0].Id);
        }

        [Theory]
        [InlineData("createdat", true)]
        [InlineData("createdat", false)]
        [InlineData("status", true)]
        [InlineData("invitedby", false)]
        [InlineData("invitedby", true)]
        public async Task GetReceivedFriendRequests_WalkingAllPages_ReturnsEveryRequestOnceInOrder(string sortBy, bool descending)
        {
            var ct = TestContext.Current.CancellationToken;
            // Every request has the same status, and pairs share CreatedAt, so the Id tie breaker is needed
            var (user, _) = await CreateReceivedRequestsAsync(["s7", "s6", "s5", "s4", "s3", "s2", "s1"], ct, minutesApart: i => i / 2);

            var all = await repository.GetReceivedFriendRequestsAsync(new PageRequest { SortBy = sortBy, Descending = descending, PageSize = 50 }, user.Id, ct);
            var pages = await WalkAllPagesAsync(r => repository.GetReceivedFriendRequestsAsync(r, user.Id, ct), new PageRequest { SortBy = sortBy, Descending = descending, PageSize = 3 });

            pages.Select(p => p.Items.Count).Should().Equal(3, 3, 1);
            pages.Should().OnlyContain(p => p.Total == 7);
            var walked = pages.SelectMany(p => p.Items).Select(r => r.Id).ToList();
            walked.Should().OnlyHaveUniqueItems();
            walked.Should().Equal(all.Items.Select(r => r.Id));
        }

        #endregion

        #region GetSentFriendRequestsAsync

        [Fact]
        public async Task GetSentFriendRequests_UserWithoutRequests_ReturnsEmptyPage()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);

            var result = await repository.GetSentFriendRequestsAsync(new PageRequest(), user.Id, ct);

            result.Items.Should().BeEmpty();
            result.Total.Should().Be(0);
            result.Next.Should().BeNull();
        }

        [Fact]
        public async Task GetSentFriendRequests_ReturnsMappedDtoWithReceiverData()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var receiver = await CreateUserAsync("receiver", ct);
            var request = await CreateRequestAsync(user.Id, receiver.Id, InvitationStatus.Pending, BaseDate, ct);

            var result = await repository.GetSentFriendRequestsAsync(new PageRequest(), user.Id, ct);

            var dto = result.Items.Should().ContainSingle().Subject;
            dto.Id.Should().Be(request.Id);
            dto.UserId.Should().Be(receiver.Id);
            dto.Username.Should().Be("receiver");
            // The sender is the user asking, so their data is not needed
            dto.InvitedByUsername.Should().BeEmpty();
            dto.Status.Should().Be(InvitationStatus.Pending);
            dto.CreatedAt.Should().BeCloseTo(request.CreatedAt, TimeSpan.FromMilliseconds(1));
            dto.ModifiedAt.Should().BeCloseTo(request.ModifiedAt, TimeSpan.FromMilliseconds(1));
            result.Total.Should().Be(1);
        }

        [Fact]
        public async Task GetSentFriendRequests_ReturnsOnlyPendingRequestsSentByUser()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var pendingReceiver = await CreateUserAsync("pending", ct);
            var other = await CreateUserAsync("other", ct);
            var pending = await CreateRequestAsync(user.Id, pendingReceiver.Id, InvitationStatus.Pending, BaseDate, ct);
            foreach (var status in new[] { InvitationStatus.Accepted, InvitationStatus.Rejected, InvitationStatus.Revoked })
            {
                var receiver = await CreateUserAsync(status.ToString(), ct);
                await CreateRequestAsync(user.Id, receiver.Id, status, BaseDate, ct);
            }
            // Received by the user, not sent
            await CreateRequestAsync(other.Id, user.Id, InvitationStatus.Pending, BaseDate, ct);
            // Sent by someone else
            await CreateRequestAsync(other.Id, pendingReceiver.Id, InvitationStatus.Pending, BaseDate, ct);

            var result = await repository.GetSentFriendRequestsAsync(new PageRequest(), user.Id, ct);

            result.Items.Select(r => r.Id).Should().Equal(pending.Id);
            result.Total.Should().Be(1);
        }

        [Fact]
        public async Task GetSentFriendRequests_DefaultRequest_SortsByCreatedAtDescending()
        {
            var ct = TestContext.Current.CancellationToken;
            var (user, requests) = await CreateSentRequestsAsync(["b", "c", "a"], ct);

            var result = await repository.GetSentFriendRequestsAsync(new PageRequest(), user.Id, ct);

            result.Items.Select(r => r.Id).Should().Equal(requests[2].Id, requests[1].Id, requests[0].Id);
        }

        [Theory]
        [InlineData("username", false)]
        [InlineData("UserName", false)]
        [InlineData("username", true)]
        public async Task GetSentFriendRequests_SortByUsername_SortsReceiversIgnoringCaseAndAccents(string sortBy, bool descending)
        {
            var ct = TestContext.Current.CancellationToken;
            var (user, _) = await CreateSentRequestsAsync(["Zeta", "beta", "Álvaro", "alfa"], ct);

            var request = new PageRequest { SortBy = sortBy, Descending = descending };
            var result = await repository.GetSentFriendRequestsAsync(request, user.Id, ct);

            var expected = new[] { "alfa", "Álvaro", "beta", "Zeta" };
            result.Items.Select(r => r.Username).Should().Equal(descending ? Enumerable.Reverse(expected) : expected);
        }

        [Theory]
        [InlineData("invitedby")]
        [InlineData("unknown")]
        [InlineData(null)]
        public async Task GetSentFriendRequests_UnknownSortBy_FallsBackToCreatedAt(string? sortBy)
        {
            var ct = TestContext.Current.CancellationToken;
            var (user, requests) = await CreateSentRequestsAsync(["b", "c", "a"], ct);

            var request = new PageRequest { SortBy = sortBy };
            var result = await repository.GetSentFriendRequestsAsync(request, user.Id, ct);

            result.Items.Select(r => r.Id).Should().Equal(requests[2].Id, requests[1].Id, requests[0].Id);
        }

        [Theory]
        [InlineData("createdat", true)]
        [InlineData("createdat", false)]
        [InlineData("status", false)]
        [InlineData("username", false)]
        [InlineData("username", true)]
        public async Task GetSentFriendRequests_WalkingAllPages_ReturnsEveryRequestOnceInOrder(string sortBy, bool descending)
        {
            var ct = TestContext.Current.CancellationToken;
            var (user, _) = await CreateSentRequestsAsync(["r7", "r6", "r5", "r4", "r3", "r2", "r1"], ct, minutesApart: i => i / 2);

            var all = await repository.GetSentFriendRequestsAsync(new PageRequest { SortBy = sortBy, Descending = descending, PageSize = 50 }, user.Id, ct);
            var pages = await WalkAllPagesAsync(r => repository.GetSentFriendRequestsAsync(r, user.Id, ct), new PageRequest { SortBy = sortBy, Descending = descending, PageSize = 3 });

            pages.Select(p => p.Items.Count).Should().Equal(3, 3, 1);
            pages.Should().OnlyContain(p => p.Total == 7);
            var walked = pages.SelectMany(p => p.Items).Select(r => r.Id).ToList();
            walked.Should().OnlyHaveUniqueItems();
            walked.Should().Equal(all.Items.Select(r => r.Id));
        }

        #endregion

        #region AcceptFriendRequestAsync

        [Fact]
        public async Task AcceptFriendRequest_Pending_CreatesRelationAndMarksRequestAsAccepted()
        {
            var ct = TestContext.Current.CancellationToken;
            var request = await CreatePendingRequestAsync(ct);

            var before = DateTime.UtcNow;
            var relationId = await repository.AcceptFriendRequestAsync(request, ct);

            relationId.Should().NotBeNull();
            var relations = await GetRelationsAsync(request.InvitedBy, request.UserId, ct);
            var relation = relations.Should().ContainSingle().Subject;
            relation.Id.Should().Be(relationId!.Value);
            relation.FriendRequestId.Should().Be(request.Id);
            relation.Deleted.Should().BeFalse();
            relation.CreatedAt.Should().BeOnOrAfter(before.AddMilliseconds(-1));

            var stored = await repository.GetFriendRequestByIdAsync(request.Id, ct);
            stored!.Status.Should().Be(InvitationStatus.Accepted);
            stored.ModifiedAt.Should().BeCloseTo(relation.CreatedAt, TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public async Task AcceptFriendRequest_Pending_StoresUsersWithSmallerIdFirst()
        {
            var ct = TestContext.Current.CancellationToken;
            var request = await CreatePendingRequestAsync(ct);

            await repository.AcceptFriendRequestAsync(request, ct);

            // PostgreSQL compares uuids byte by byte, which matches the order of their lowercase string form
            var ordered = new[] { request.InvitedBy, request.UserId }.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray();
            var relation = (await GetRelationsAsync(request.InvitedBy, request.UserId, ct)).Single();
            relation.User1Id.Should().Be(ordered[0]);
            relation.User2Id.Should().Be(ordered[1]);
            var conversation = (await GetDirectConversationsAsync(request.InvitedBy, request.UserId, ct)).Single();
            conversation.User1Id.Should().Be(ordered[0]);
            conversation.User2Id.Should().Be(ordered[1]);
        }

        [Fact]
        public async Task AcceptFriendRequest_Pending_CreatesDirectConversation()
        {
            var ct = TestContext.Current.CancellationToken;
            var request = await CreatePendingRequestAsync(ct);

            await repository.AcceptFriendRequestAsync(request, ct);

            var conversation = (await GetDirectConversationsAsync(request.InvitedBy, request.UserId, ct)).Should().ContainSingle().Subject;
            var relation = (await GetRelationsAsync(request.InvitedBy, request.UserId, ct)).Single();
            conversation.ChatConversationId.Should().NotBeEmpty();
            conversation.CreatedAt.Should().BeCloseTo(relation.CreatedAt, TimeSpan.FromMilliseconds(1));
            (await GetConversationTypeAsync(conversation.ChatConversationId, ct)).Should().Be((short)ChatConversationType.Direct);
        }

        [Fact]
        public async Task AcceptFriendRequest_Pending_RemovesItFromPendingLists()
        {
            var ct = TestContext.Current.CancellationToken;
            var request = await CreatePendingRequestAsync(ct);

            await repository.AcceptFriendRequestAsync(request, ct);

            (await repository.GetReceivedFriendRequestsAsync(new PageRequest(), request.UserId, ct)).Items.Should().BeEmpty();
            (await repository.GetSentFriendRequestsAsync(new PageRequest(), request.InvitedBy, ct)).Items.Should().BeEmpty();
        }

        [Theory]
        [InlineData(InvitationStatus.Accepted)]
        [InlineData(InvitationStatus.Rejected)]
        [InlineData(InvitationStatus.Revoked)]
        public async Task AcceptFriendRequest_NotPending_ReturnsNullAndChangesNothing(InvitationStatus status)
        {
            var ct = TestContext.Current.CancellationToken;
            var sender = await CreateUserAsync("sender", ct);
            var receiver = await CreateUserAsync("receiver", ct);
            var request = await CreateRequestAsync(sender.Id, receiver.Id, status, BaseDate, ct);

            var relationId = await repository.AcceptFriendRequestAsync(request, ct);

            relationId.Should().BeNull();
            (await GetRelationsAsync(sender.Id, receiver.Id, ct)).Should().BeEmpty();
            (await GetDirectConversationsAsync(sender.Id, receiver.Id, ct)).Should().BeEmpty();
            (await CountConversationsAsync(ct)).Should().Be(0);
            var stored = await repository.GetFriendRequestByIdAsync(request.Id, ct);
            stored!.Status.Should().Be(status);
            stored.ModifiedAt.Should().BeCloseTo(BaseDate, TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public async Task AcceptFriendRequest_BySender_ReturnsNullAndChangesNothing()
        {
            var ct = TestContext.Current.CancellationToken;
            var request = await CreatePendingRequestAsync(ct);
            // Only the receiver can accept: the sender tries to accept their own request
            var forged = new FriendRequest
            {
                Id = request.Id,
                InvitedBy = request.InvitedBy,
                UserId = request.InvitedBy,
                Status = request.Status
            };

            var relationId = await repository.AcceptFriendRequestAsync(forged, ct);

            relationId.Should().BeNull();
            (await GetRelationsAsync(request.InvitedBy, request.UserId, ct)).Should().BeEmpty();
            (await CountConversationsAsync(ct)).Should().Be(0);
            (await repository.GetFriendRequestByIdAsync(request.Id, ct))!.Status.Should().Be(InvitationStatus.Pending);
        }

        [Fact]
        public async Task AcceptFriendRequest_DifferentUser_ReturnsNullAndChangesNothing()
        {
            var ct = TestContext.Current.CancellationToken;
            var request = await CreatePendingRequestAsync(ct);
            var intruder = await CreateUserAsync("intruder", ct);
            var forged = new FriendRequest
            {
                Id = request.Id,
                InvitedBy = request.InvitedBy,
                UserId = intruder.Id,
                Status = request.Status
            };

            var relationId = await repository.AcceptFriendRequestAsync(forged, ct);

            relationId.Should().BeNull();
            (await GetRelationsAsync(request.InvitedBy, intruder.Id, ct)).Should().BeEmpty();
            (await GetRelationsAsync(request.InvitedBy, request.UserId, ct)).Should().BeEmpty();
            (await repository.GetFriendRequestByIdAsync(request.Id, ct))!.Status.Should().Be(InvitationStatus.Pending);
        }

        [Fact]
        public async Task AcceptFriendRequest_NotExisting_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var request = await CreatePendingRequestAsync(ct);
            var missing = new FriendRequest
            {
                Id = Guid.NewGuid(),
                InvitedBy = request.InvitedBy,
                UserId = request.UserId
            };

            var relationId = await repository.AcceptFriendRequestAsync(missing, ct);

            relationId.Should().BeNull();
            (await GetRelationsAsync(request.InvitedBy, request.UserId, ct)).Should().BeEmpty();
            (await CountConversationsAsync(ct)).Should().Be(0);
        }

        [Fact]
        public async Task AcceptFriendRequest_Twice_SecondReturnsNullAndKeepsSingleRelationAndConversation()
        {
            var ct = TestContext.Current.CancellationToken;
            var request = await CreatePendingRequestAsync(ct);

            var first = await repository.AcceptFriendRequestAsync(request, ct);
            var second = await repository.AcceptFriendRequestAsync(request, ct);

            first.Should().NotBeNull();
            second.Should().BeNull();
            (await GetRelationsAsync(request.InvitedBy, request.UserId, ct)).Select(r => r.Id).Should().Equal(first!.Value);
            (await GetDirectConversationsAsync(request.InvitedBy, request.UserId, ct)).Should().ContainSingle();
            (await CountConversationsAsync(ct)).Should().Be(1);
        }

        [Fact]
        public async Task AcceptFriendRequest_Concurrently_OnlyOneSucceeds()
        {
            var ct = TestContext.Current.CancellationToken;
            var request = await CreatePendingRequestAsync(ct);

            var results = await Task.WhenAll(Enumerable.Range(0, 5)
                .Select(_ => repository.AcceptFriendRequestAsync(request, ct)));

            results.Count(r => r is not null).Should().Be(1);
            (await GetRelationsAsync(request.InvitedBy, request.UserId, ct)).Should().ContainSingle();
            (await GetDirectConversationsAsync(request.InvitedBy, request.UserId, ct)).Should().ContainSingle();
            (await CountConversationsAsync(ct)).Should().Be(1);
        }

        [Fact]
        public async Task AcceptFriendRequest_AlreadyFriends_ReturnsNullAndKeepsRequestPending()
        {
            var ct = TestContext.Current.CancellationToken;
            var first = await CreatePendingRequestAsync(ct);
            var relationId = await repository.AcceptFriendRequestAsync(first, ct);
            // The backend should not allow it, but the database does: the previous request is no longer pending
            var second = await CreateRequestAsync(first.UserId, first.InvitedBy, InvitationStatus.Pending, BaseDate.AddMinutes(1), ct);

            var result = await repository.AcceptFriendRequestAsync(second, ct);

            result.Should().BeNull();
            (await GetRelationsAsync(first.InvitedBy, first.UserId, ct)).Select(r => r.Id).Should().Equal(relationId!.Value);
            (await GetDirectConversationsAsync(first.InvitedBy, first.UserId, ct)).Should().ContainSingle();
            (await repository.GetFriendRequestByIdAsync(second.Id, ct))!.Status.Should().Be(InvitationStatus.Pending);
        }

        [Fact]
        public async Task AcceptFriendRequest_FriendsAgainAfterUnfriending_CreatesNewRelationAndKeepsConversation()
        {
            var ct = TestContext.Current.CancellationToken;
            var first = await CreatePendingRequestAsync(ct);
            var previousRelationId = await repository.AcceptFriendRequestAsync(first, ct);
            var previousConversation = (await GetDirectConversationsAsync(first.InvitedBy, first.UserId, ct)).Single();
            await MarkRelationAsDeletedAsync(previousRelationId!.Value, ct);
            var second = await CreateRequestAsync(first.UserId, first.InvitedBy, InvitationStatus.Pending, BaseDate.AddMinutes(1), ct);

            var relationId = await repository.AcceptFriendRequestAsync(second, ct);

            relationId.Should().NotBeNull().And.NotBe(previousRelationId);
            var relations = await GetRelationsAsync(first.InvitedBy, first.UserId, ct);
            relations.Should().HaveCount(2);
            relations.Single(r => r.Id == previousRelationId).Deleted.Should().BeTrue();
            relations.Single(r => r.Id == relationId).Deleted.Should().BeFalse();
            relations.Single(r => r.Id == relationId).FriendRequestId.Should().Be(second.Id);
            // They get their old messages back: same direct conversation, no new ChatConversation
            var conversation = (await GetDirectConversationsAsync(first.InvitedBy, first.UserId, ct)).Should().ContainSingle().Subject;
            conversation.Id.Should().Be(previousConversation.Id);
            conversation.ChatConversationId.Should().Be(previousConversation.ChatConversationId);
            (await CountConversationsAsync(ct)).Should().Be(1);
            (await repository.GetFriendRequestByIdAsync(second.Id, ct))!.Status.Should().Be(InvitationStatus.Accepted);
        }

        [Fact]
        public async Task AcceptFriendRequest_DifferentPairs_EachGetsItsOwnConversation()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var first = await CreateUserAsync("first", ct);
            var second = await CreateUserAsync("second", ct);
            var firstRequest = await CreateRequestAsync(first.Id, user.Id, InvitationStatus.Pending, BaseDate, ct);
            var secondRequest = await CreateRequestAsync(user.Id, second.Id, InvitationStatus.Pending, BaseDate, ct);

            await repository.AcceptFriendRequestAsync(firstRequest, ct);
            await repository.AcceptFriendRequestAsync(secondRequest, ct);

            var firstConversation = (await GetDirectConversationsAsync(user.Id, first.Id, ct)).Single();
            var secondConversation = (await GetDirectConversationsAsync(user.Id, second.Id, ct)).Single();
            firstConversation.ChatConversationId.Should().NotBe(secondConversation.ChatConversationId);
            (await CountConversationsAsync(ct)).Should().Be(2);
        }

        #endregion

        #region Cancellation

        [Theory]
        [InlineData(nameof(IFriendRequestRepository.GetFriendRequestByIdAsync))]
        [InlineData(nameof(IFriendRequestRepository.GetReceivedFriendRequestsAsync))]
        [InlineData(nameof(IFriendRequestRepository.GetSentFriendRequestsAsync))]
        [InlineData(nameof(IFriendRequestRepository.AcceptFriendRequestAsync))]
        public async Task AnyMethod_CancelledToken_ThrowsOperationCanceled(string method)
        {
            var ct = TestContext.Current.CancellationToken;
            var request = await CreatePendingRequestAsync(ct);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            Func<Task> act = method switch
            {
                nameof(IFriendRequestRepository.GetFriendRequestByIdAsync) => () => repository.GetFriendRequestByIdAsync(request.Id, cts.Token),
                nameof(IFriendRequestRepository.GetReceivedFriendRequestsAsync) => () => repository.GetReceivedFriendRequestsAsync(new PageRequest(), request.UserId, cts.Token),
                nameof(IFriendRequestRepository.GetSentFriendRequestsAsync) => () => repository.GetSentFriendRequestsAsync(new PageRequest(), request.InvitedBy, cts.Token),
                nameof(IFriendRequestRepository.AcceptFriendRequestAsync) => () => repository.AcceptFriendRequestAsync(request, cts.Token),
                _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
            };

            // OperationCanceledException is excluded from the wrapping catch in AcceptFriendRequestAsync
            await act.Should().ThrowAsync<OperationCanceledException>();
            (await GetRelationsAsync(request.InvitedBy, request.UserId, ct)).Should().BeEmpty();
            (await CountConversationsAsync(ct)).Should().Be(0);
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

        private async Task<FriendRequest> CreateRequestAsync(Guid invitedBy, Guid userId, InvitationStatus status, DateTime createdAt, CancellationToken ct)
        {
            var request = Persistable.CreateNew<FriendRequest>();
            request.InvitedBy = invitedBy;
            request.UserId = userId;
            request.Status = status;
            request.CreatedAt = createdAt;
            request.ModifiedAt = createdAt;
            await requestSaver.SaveAsync(request, ct);
            return request;
        }

        private async Task<FriendRequest> CreatePendingRequestAsync(CancellationToken ct)
        {
            var sender = await CreateUserAsync("sender", ct);
            var receiver = await CreateUserAsync("receiver", ct);
            return await CreateRequestAsync(sender.Id, receiver.Id, InvitationStatus.Pending, BaseDate, ct);
        }

        /// <summary>
        /// Creates one user with a pending request from each sender, in the given order. By default they are one minute apart.
        /// </summary>
        private async Task<(UserInfo User, List<FriendRequest> Requests)> CreateReceivedRequestsAsync(
            string[] senders, CancellationToken ct, Func<int, int>? minutesApart = null)
        {
            var user = await CreateUserAsync("user", ct);
            var requests = new List<FriendRequest>();
            for (var i = 0; i < senders.Length; i++)
            {
                var sender = await CreateUserAsync(senders[i], ct);
                var createdAt = BaseDate.AddMinutes(minutesApart?.Invoke(i) ?? i);
                requests.Add(await CreateRequestAsync(sender.Id, user.Id, InvitationStatus.Pending, createdAt, ct));
            }

            return (user, requests);
        }

        /// <summary>
        /// Creates one user with a pending request to each receiver, in the given order. By default they are one minute apart.
        /// </summary>
        private async Task<(UserInfo User, List<FriendRequest> Requests)> CreateSentRequestsAsync(
            string[] receivers, CancellationToken ct, Func<int, int>? minutesApart = null)
        {
            var user = await CreateUserAsync("user", ct);
            var requests = new List<FriendRequest>();
            for (var i = 0; i < receivers.Length; i++)
            {
                var receiver = await CreateUserAsync(receivers[i], ct);
                var createdAt = BaseDate.AddMinutes(minutesApart?.Invoke(i) ?? i);
                requests.Add(await CreateRequestAsync(user.Id, receiver.Id, InvitationStatus.Pending, createdAt, ct));
            }

            return (user, requests);
        }

        private async Task MarkRelationAsDeletedAsync(Guid relationId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                """UPDATE "HootOut"."FriendRelation" SET "Deleted" = true WHERE "Id" = @Id""",
                new { Id = relationId },
                cancellationToken: ct));
        }

        private async Task<List<FriendRelationRow>> GetRelationsAsync(Guid userA, Guid userB, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return (await conn.QueryAsync<FriendRelationRow>(new CommandDefinition(
                """
                    SELECT "Id", "User1Id", "User2Id", "FriendRequestId", "Deleted", "CreatedAt"
                    FROM "HootOut"."FriendRelation"
                    WHERE ("User1Id" = @A AND "User2Id" = @B) OR ("User1Id" = @B AND "User2Id" = @A)
                """,
                new { A = userA, B = userB },
                cancellationToken: ct))).ToList();
        }

        private async Task<List<DirectConversationRow>> GetDirectConversationsAsync(Guid userA, Guid userB, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return (await conn.QueryAsync<DirectConversationRow>(new CommandDefinition(
                """
                    SELECT "Id", "ChatConversationId", "User1Id", "User2Id", "CreatedAt"
                    FROM "HootOut"."DirectConversation"
                    WHERE ("User1Id" = @A AND "User2Id" = @B) OR ("User1Id" = @B AND "User2Id" = @A)
                """,
                new { A = userA, B = userB },
                cancellationToken: ct))).ToList();
        }

        private async Task<short?> GetConversationTypeAsync(Guid conversationId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.ExecuteScalarAsync<short?>(new CommandDefinition(
                """SELECT "ConversationType" FROM "HootOut"."ChatConversation" WHERE "Id" = @Id""",
                new { Id = conversationId },
                cancellationToken: ct));
        }

        private async Task<long> CountConversationsAsync(CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
                """SELECT COUNT(*) FROM "HootOut"."ChatConversation" """,
                cancellationToken: ct));
        }

        private static async Task<List<PageResult<FriendRequestDto>>> WalkAllPagesAsync(
            Func<PageRequest, Task<PageResult<FriendRequestDto>>> getPage, PageRequest firstRequest)
        {
            var pages = new List<PageResult<FriendRequestDto>>();
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

        private sealed class FriendRelationRow
        {
            public Guid Id { get; set; }
            public Guid User1Id { get; set; }
            public Guid User2Id { get; set; }
            public Guid? FriendRequestId { get; set; }
            public bool Deleted { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        private sealed class DirectConversationRow
        {
            public Guid Id { get; set; }
            public Guid ChatConversationId { get; set; }
            public Guid User1Id { get; set; }
            public Guid User2Id { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}
