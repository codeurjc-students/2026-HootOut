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
    public class FriendRelationRepositoryTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private static readonly DateTime BaseDate = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private readonly IContainer container;

        private readonly ClearAllTables clearTables;

        private readonly IFriendRelationRepository repository;

        private readonly IFriendRequestRepository requestRepository;

        private readonly ISaver<FriendRequest> requestSaver;

        private readonly ISaver<UserInfo> userSaver;

        private readonly IPersistenceProvider provider;

        public FriendRelationRepositoryTests(PostgresTestContainer postgresContainer)
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

            repository = container.Resolve<IFriendRelationRepository>();
            requestRepository = container.Resolve<IFriendRequestRepository>();
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

        #region GetUserFriendsAsync - filtering and mapping

        [Fact]
        public async Task GetUserFriends_UserWithoutFriends_ReturnsEmptyPage()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);

            var result = await repository.GetUserFriendsAsync(new PageRequest(), user.Id, ct);

            result.Items.Should().BeEmpty();
            result.Total.Should().Be(0);
            result.Next.Should().BeNull();
        }

        [Fact]
        public async Task GetUserFriends_ReturnsMappedDtoWithFriendAndConversation()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var friend = await CreateUserAsync("friend", ct);
            var relationId = await MakeFriendsAsync(friend, user, BaseDate, ct);
            var conversation = await GetDirectConversationAsync(user.Id, friend.Id, ct);

            var result = await repository.GetUserFriendsAsync(new PageRequest(), user.Id, ct);

            var dto = result.Items.Should().ContainSingle().Subject;
            dto.Id.Should().Be(relationId);
            dto.Friend.Should().NotBeNull();
            dto.Friend.Id.Should().Be(friend.Id);
            dto.Friend.Username.Should().Be("friend");
            dto.ChatConversationId.Should().Be(conversation.ChatConversationId);
            dto.CreatedAt.Should().BeCloseTo(BaseDate, TimeSpan.FromMilliseconds(1));
            result.Total.Should().Be(1);
        }

        [Fact]
        public async Task GetUserFriends_BothUsers_SeeEachOtherAsFriend()
        {
            var ct = TestContext.Current.CancellationToken;
            var first = await CreateUserAsync("first", ct);
            var second = await CreateUserAsync("second", ct);
            var relationId = await MakeFriendsAsync(first, second, BaseDate, ct);

            var firstResult = await repository.GetUserFriendsAsync(new PageRequest(), first.Id, ct);
            var secondResult = await repository.GetUserFriendsAsync(new PageRequest(), second.Id, ct);

            // One of them is User1Id and the other User2Id: the friend is always the other user
            firstResult.Items.Should().ContainSingle().Which.Friend.Id.Should().Be(second.Id);
            secondResult.Items.Should().ContainSingle().Which.Friend.Id.Should().Be(first.Id);
            firstResult.Items[0].Id.Should().Be(relationId);
            secondResult.Items[0].Id.Should().Be(relationId);
            firstResult.Items[0].ChatConversationId.Should().Be(secondResult.Items[0].ChatConversationId);
        }

        [Fact]
        public async Task GetUserFriends_SeveralFriends_ReturnsEveryFriendOnceAndNeverTheUser()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var friends = new List<UserInfo>();
            for (var i = 0; i < 6; i++)
            {
                var friend = await CreateUserAsync($"friend{i}", ct);
                // Half the requests sent by the user and half received, so the user is in both columns
                if (i % 2 == 0)
                    await MakeFriendsAsync(user, friend, BaseDate.AddMinutes(i), ct);
                else
                    await MakeFriendsAsync(friend, user, BaseDate.AddMinutes(i), ct);
                friends.Add(friend);
            }

            var result = await repository.GetUserFriendsAsync(new PageRequest(), user.Id, ct);

            result.Items.Select(f => f.Friend.Id).Should().BeEquivalentTo(friends.Select(f => f.Id));
            result.Items.Should().NotContain(f => f.Friend.Id == user.Id);
            result.Items.Select(f => f.ChatConversationId).Should().OnlyHaveUniqueItems();
            result.Total.Should().Be(6);
        }

        [Fact]
        public async Task GetUserFriends_FriendshipsOfOtherUsers_AreExcluded()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var friend = await CreateUserAsync("friend", ct);
            var other = await CreateUserAsync("other", ct);
            var stranger = await CreateUserAsync("stranger", ct);
            await MakeFriendsAsync(user, friend, BaseDate, ct);
            await MakeFriendsAsync(friend, other, BaseDate, ct);
            await MakeFriendsAsync(other, stranger, BaseDate, ct);

            var result = await repository.GetUserFriendsAsync(new PageRequest(), user.Id, ct);

            result.Items.Select(f => f.Friend.Id).Should().Equal(friend.Id);
            result.Total.Should().Be(1);
        }

        [Theory]
        [InlineData(InvitationStatus.Pending)]
        [InlineData(InvitationStatus.Rejected)]
        [InlineData(InvitationStatus.Revoked)]
        public async Task GetUserFriends_RequestNotAccepted_IsNotAFriend(InvitationStatus status)
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var other = await CreateUserAsync("other", ct);
            await CreateRequestAsync(other.Id, user.Id, status, ct);

            var result = await repository.GetUserFriendsAsync(new PageRequest(), user.Id, ct);

            result.Items.Should().BeEmpty();
            result.Total.Should().Be(0);
        }

        [Fact]
        public async Task GetUserFriends_DeletedRelation_IsExcludedFromItemsAndTotal()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var kept = await CreateUserAsync("kept", ct);
            var removed = await CreateUserAsync("removed", ct);
            await MakeFriendsAsync(user, kept, BaseDate, ct);
            var removedRelationId = await MakeFriendsAsync(removed, user, BaseDate.AddMinutes(1), ct);
            await MarkRelationAsDeletedAsync(removedRelationId, ct);

            var result = await repository.GetUserFriendsAsync(new PageRequest(), user.Id, ct);

            result.Items.Select(f => f.Friend.Id).Should().Equal(kept.Id);
            result.Total.Should().Be(1);
        }

        [Fact]
        public async Task GetUserFriends_FriendsAgainAfterUnfriending_ReturnsOnlyNewRelationWithSameConversation()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var friend = await CreateUserAsync("friend", ct);
            var previousRelationId = await MakeFriendsAsync(user, friend, BaseDate, ct);
            var conversation = await GetDirectConversationAsync(user.Id, friend.Id, ct);
            await MarkRelationAsDeletedAsync(previousRelationId, ct);
            var relationId = await MakeFriendsAsync(friend, user, BaseDate.AddMinutes(1), ct);

            var result = await repository.GetUserFriendsAsync(new PageRequest(), user.Id, ct);

            var dto = result.Items.Should().ContainSingle().Subject;
            dto.Id.Should().Be(relationId);
            dto.ChatConversationId.Should().Be(conversation.ChatConversationId);
            result.Total.Should().Be(1);
        }

        #endregion

        #region GetUserFriendsAsync - sorting

        [Fact]
        public async Task GetUserFriends_DefaultRequest_SortsByCreatedAtDescending()
        {
            var ct = TestContext.Current.CancellationToken;
            var (user, relations) = await CreateUserWithFriendsAsync(["b", "c", "a"], ct);

            var result = await repository.GetUserFriendsAsync(new PageRequest(), user.Id, ct);

            result.Items.Select(f => f.Id).Should().Equal(relations[2], relations[1], relations[0]);
        }

        [Fact]
        public async Task GetUserFriends_CreatedAtAscending_SortsOldestFirst()
        {
            var ct = TestContext.Current.CancellationToken;
            var (user, relations) = await CreateUserWithFriendsAsync(["b", "c", "a"], ct);

            var request = new PageRequest { SortBy = "createdat", Descending = false };
            var result = await repository.GetUserFriendsAsync(request, user.Id, ct);

            result.Items.Select(f => f.Id).Should().Equal(relations[0], relations[1], relations[2]);
        }

        [Theory]
        [InlineData("username", false)]
        [InlineData("UserName", false)]
        [InlineData("username", true)]
        public async Task GetUserFriends_SortByUsername_SortsFriendsIgnoringCaseAndAccents(string sortBy, bool descending)
        {
            var ct = TestContext.Current.CancellationToken;
            var (user, _) = await CreateUserWithFriendsAsync(["Zeta", "beta", "Álvaro", "alfa"], ct);

            var request = new PageRequest { SortBy = sortBy, Descending = descending };
            var result = await repository.GetUserFriendsAsync(request, user.Id, ct);

            var expected = new[] { "alfa", "Álvaro", "beta", "Zeta" };
            result.Items.Select(f => f.Friend.Username).Should().Equal(descending ? Enumerable.Reverse(expected) : expected);
        }

        [Theory]
        [InlineData("status")]
        [InlineData("unknown")]
        [InlineData("")]
        [InlineData(null)]
        public async Task GetUserFriends_UnknownSortBy_FallsBackToCreatedAt(string? sortBy)
        {
            var ct = TestContext.Current.CancellationToken;
            var (user, relations) = await CreateUserWithFriendsAsync(["b", "c", "a"], ct);

            var request = new PageRequest { SortBy = sortBy };
            var result = await repository.GetUserFriendsAsync(request, user.Id, ct);

            result.Items.Select(f => f.Id).Should().Equal(relations[2], relations[1], relations[0]);
        }

        #endregion

        #region GetUserFriendsAsync - pagination

        [Fact]
        public async Task GetUserFriends_FirstPage_ReturnsPageSizeItemsAndNextCursor()
        {
            var ct = TestContext.Current.CancellationToken;
            var (user, relations) = await CreateUserWithFriendsAsync(["a", "b", "c", "d", "e"], ct);

            var result = await repository.GetUserFriendsAsync(new PageRequest { PageSize = 2 }, user.Id, ct);

            result.Items.Select(f => f.Id).Should().Equal(relations[4], relations[3]);
            result.Next.Should().Be(relations[3]);
            result.Total.Should().Be(5);
        }

        [Theory]
        [InlineData("createdat", true)]
        [InlineData("createdat", false)]
        [InlineData("username", false)]
        [InlineData("username", true)]
        public async Task GetUserFriends_WalkingAllPages_ReturnsEveryFriendOnceInOrder(string sortBy, bool descending)
        {
            var ct = TestContext.Current.CancellationToken;
            // Pairs share CreatedAt, so the Id tie breaker is needed; the cursor subquery must return a single row
            var (user, _) = await CreateUserWithFriendsAsync(["f7", "f6", "f5", "f4", "f3", "f2", "f1"], ct, minutesApart: i => i / 2);

            var all = await repository.GetUserFriendsAsync(new PageRequest { SortBy = sortBy, Descending = descending, PageSize = 50 }, user.Id, ct);
            var pages = await WalkAllPagesAsync(r => repository.GetUserFriendsAsync(r, user.Id, ct), new PageRequest { SortBy = sortBy, Descending = descending, PageSize = 3 });

            pages.Select(p => p.Items.Count).Should().Equal(3, 3, 1);
            pages.Should().OnlyContain(p => p.Total == 7);
            var walked = pages.SelectMany(p => p.Items).Select(f => f.Id).ToList();
            walked.Should().OnlyHaveUniqueItems();
            walked.Should().Equal(all.Items.Select(f => f.Id));
        }

        [Fact]
        public async Task GetUserFriends_WalkingAllPages_SkipsDeletedRelations()
        {
            var ct = TestContext.Current.CancellationToken;
            var (user, relations) = await CreateUserWithFriendsAsync(["a", "b", "c", "d", "e", "f"], ct);
            await MarkRelationAsDeletedAsync(relations[1], ct);
            await MarkRelationAsDeletedAsync(relations[4], ct);

            var pages = await WalkAllPagesAsync(r => repository.GetUserFriendsAsync(r, user.Id, ct), new PageRequest { PageSize = 2 });

            pages.SelectMany(p => p.Items).Select(f => f.Id).Should().Equal(relations[5], relations[3], relations[2], relations[0]);
            pages.Should().OnlyContain(p => p.Total == 4);
        }

        #endregion

        #region GetDirectConverationByConversationAndUserId

        [Fact]
        public async Task GetDirectConversation_Friends_ReturnsDirectConversationIdForBothUsers()
        {
            var ct = TestContext.Current.CancellationToken;
            var first = await CreateUserAsync("first", ct);
            var second = await CreateUserAsync("second", ct);
            await MakeFriendsAsync(first, second, BaseDate, ct);
            var conversation = await GetDirectConversationAsync(first.Id, second.Id, ct);

            var firstResult = await repository.GetDirectConverationByConversationAndUserId(conversation.ChatConversationId, first.Id, ct);
            var secondResult = await repository.GetDirectConverationByConversationAndUserId(conversation.ChatConversationId, second.Id, ct);

            firstResult.Should().Be(conversation.Id);
            secondResult.Should().Be(conversation.Id);
        }

        [Fact]
        public async Task GetDirectConversation_NotParticipant_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var first = await CreateUserAsync("first", ct);
            var second = await CreateUserAsync("second", ct);
            var intruder = await CreateUserAsync("intruder", ct);
            await MakeFriendsAsync(first, second, BaseDate, ct);
            // The intruder is friends with both, but the conversation is not theirs
            await MakeFriendsAsync(intruder, first, BaseDate, ct);
            await MakeFriendsAsync(second, intruder, BaseDate, ct);
            var conversation = await GetDirectConversationAsync(first.Id, second.Id, ct);

            var result = await repository.GetDirectConverationByConversationAndUserId(conversation.ChatConversationId, intruder.Id, ct);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetDirectConversation_Unfriended_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var first = await CreateUserAsync("first", ct);
            var second = await CreateUserAsync("second", ct);
            var relationId = await MakeFriendsAsync(first, second, BaseDate, ct);
            var conversation = await GetDirectConversationAsync(first.Id, second.Id, ct);
            await MarkRelationAsDeletedAsync(relationId, ct);

            var result = await repository.GetDirectConverationByConversationAndUserId(conversation.ChatConversationId, first.Id, ct);

            // The conversation is kept, but without an active friendship nobody can send to it
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetDirectConversation_FriendsAgainAfterUnfriending_ReturnsSameConversation()
        {
            var ct = TestContext.Current.CancellationToken;
            var first = await CreateUserAsync("first", ct);
            var second = await CreateUserAsync("second", ct);
            var relationId = await MakeFriendsAsync(first, second, BaseDate, ct);
            var conversation = await GetDirectConversationAsync(first.Id, second.Id, ct);
            await MarkRelationAsDeletedAsync(relationId, ct);
            await MakeFriendsAsync(second, first, BaseDate.AddMinutes(1), ct);

            var result = await repository.GetDirectConverationByConversationAndUserId(conversation.ChatConversationId, first.Id, ct);

            result.Should().Be(conversation.Id);
        }

        [Fact]
        public async Task GetDirectConversation_DirectConversationIdInsteadOfConversationId_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var first = await CreateUserAsync("first", ct);
            var second = await CreateUserAsync("second", ct);
            await MakeFriendsAsync(first, second, BaseDate, ct);
            var conversation = await GetDirectConversationAsync(first.Id, second.Id, ct);

            var result = await repository.GetDirectConverationByConversationAndUserId(conversation.Id, first.Id, ct);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetDirectConversation_ConversationWithoutDirectConversation_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var friend = await CreateUserAsync("friend", ct);
            await MakeFriendsAsync(user, friend, BaseDate, ct);
            // For example the conversation of a channel
            var conversationId = await InsertConversationAsync(ChatConversationType.Channel, ct);

            var result = await repository.GetDirectConverationByConversationAndUserId(conversationId, user.Id, ct);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetDirectConversation_NotExistingConversation_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var friend = await CreateUserAsync("friend", ct);
            await MakeFriendsAsync(user, friend, BaseDate, ct);

            var result = await repository.GetDirectConverationByConversationAndUserId(Guid.NewGuid(), user.Id, ct);

            result.Should().BeNull();
        }

        #endregion

        #region Cancellation

        [Theory]
        [InlineData(nameof(IFriendRelationRepository.GetUserFriendsAsync))]
        [InlineData(nameof(IFriendRelationRepository.GetDirectConverationByConversationAndUserId))]
        public async Task AnyMethod_CancelledToken_ThrowsOperationCanceled(string method)
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user", ct);
            var friend = await CreateUserAsync("friend", ct);
            await MakeFriendsAsync(user, friend, BaseDate, ct);
            var conversation = await GetDirectConversationAsync(user.Id, friend.Id, ct);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            Func<Task> act = method switch
            {
                nameof(IFriendRelationRepository.GetUserFriendsAsync) => () => repository.GetUserFriendsAsync(new PageRequest(), user.Id, cts.Token),
                nameof(IFriendRelationRepository.GetDirectConverationByConversationAndUserId) => () => repository.GetDirectConverationByConversationAndUserId(conversation.ChatConversationId, user.Id, cts.Token),
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

        private async Task<FriendRequest> CreateRequestAsync(Guid invitedBy, Guid userId, InvitationStatus status, CancellationToken ct)
        {
            var request = Persistable.CreateNew<FriendRequest>();
            request.InvitedBy = invitedBy;
            request.UserId = userId;
            request.Status = status;
            await requestSaver.SaveAsync(request, ct);
            return request;
        }

        /// <summary>
        /// FriendRelation has no saver: the friendship is created by accepting a request, as the backend does.
        /// The relation CreatedAt is then moved to the given date so the order does not depend on DateTime.UtcNow.
        /// </summary>
        private async Task<Guid> MakeFriendsAsync(UserInfo sender, UserInfo receiver, DateTime createdAt, CancellationToken ct)
        {
            var request = await CreateRequestAsync(sender.Id, receiver.Id, InvitationStatus.Pending, ct);
            var relationId = await requestRepository.AcceptFriendRequestAsync(request, ct);
            relationId.Should().NotBeNull();

            await using var conn = await provider.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                """UPDATE "HootOut"."FriendRelation" SET "CreatedAt" = @CreatedAt, "ModifiedAt" = @CreatedAt WHERE "Id" = @Id""",
                new { Id = relationId!.Value, CreatedAt = createdAt },
                cancellationToken: ct));

            return relationId.Value;
        }

        /// <summary>
        /// Creates one user who is friends with every given user, in the given order. By default the friendships are one minute apart.
        /// Returns the relation ids in the same order.
        /// </summary>
        private async Task<(UserInfo User, List<Guid> Relations)> CreateUserWithFriendsAsync(
            string[] friends, CancellationToken ct, Func<int, int>? minutesApart = null)
        {
            var user = await CreateUserAsync("user", ct);
            var relations = new List<Guid>();
            for (var i = 0; i < friends.Length; i++)
            {
                var friend = await CreateUserAsync(friends[i], ct);
                var createdAt = BaseDate.AddMinutes(minutesApart?.Invoke(i) ?? i);
                // Alternate who sends the request, so the user is sometimes User1Id and sometimes User2Id
                relations.Add(i % 2 == 0
                    ? await MakeFriendsAsync(user, friend, createdAt, ct)
                    : await MakeFriendsAsync(friend, user, createdAt, ct));
            }

            return (user, relations);
        }

        private async Task MarkRelationAsDeletedAsync(Guid relationId, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                """UPDATE "HootOut"."FriendRelation" SET "Deleted" = true WHERE "Id" = @Id""",
                new { Id = relationId },
                cancellationToken: ct));
        }

        private async Task<DirectConversationRow> GetDirectConversationAsync(Guid userA, Guid userB, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.QuerySingleAsync<DirectConversationRow>(new CommandDefinition(
                """
                    SELECT "Id", "ChatConversationId"
                    FROM "HootOut"."DirectConversation"
                    WHERE "User1Id" = LEAST(@A, @B) AND "User2Id" = GREATEST(@A, @B)
                """,
                new { A = userA, B = userB },
                cancellationToken: ct));
        }

        private async Task<Guid> InsertConversationAsync(ChatConversationType type, CancellationToken ct)
        {
            var id = Guid.CreateVersion7();
            await using var conn = await provider.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                """
                    INSERT INTO "HootOut"."ChatConversation" ("Id", "ConversationType", "CreatedAt", "ModifiedAt")
                    VALUES (@Id, @Type, @Now, @Now)
                """,
                new { Id = id, Type = (short)type, Now = BaseDate },
                cancellationToken: ct));
            return id;
        }

        private static async Task<List<PageResult<FriendRelationDto>>> WalkAllPagesAsync(
            Func<PageRequest, Task<PageResult<FriendRelationDto>>> getPage, PageRequest firstRequest)
        {
            var pages = new List<PageResult<FriendRelationDto>>();
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

        private sealed class DirectConversationRow
        {
            public Guid Id { get; set; }
            public Guid ChatConversationId { get; set; }
        }
    }
}
