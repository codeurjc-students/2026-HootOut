using Autofac;
using FluentAssertions;
using FluentValidation;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.Contracts.ChatConversations.Enums;
using HootOut.Contracts.ChatConversations.Services;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Enums;
using HootOut.Contracts.Friends.Dtos;
using HootOut.Contracts.Friends.Requests;
using HootOut.Contracts.Friends.Responses;
using HootOut.Contracts.Friends.Services;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.Contracts.Users.Services;
using HootOut.Friends.Repositories;

namespace HootOut.Friends.IntegrationTests.Services
{
    [Collection("IntegrationTests")]
    public class FriendsServiceTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private readonly IContainer container;
        private readonly ClearAllTables clearTables;
        private readonly IUserService userService;
        private readonly IFriendsService friendsService;
        private readonly IChatConversationsService chatConversationsService;
        private readonly IChatMessageService messageService;
        private readonly IFriendRequestRepository friendRequestRepository;
        private readonly IFriendRelationRepository friendRelationRepository;

        public FriendsServiceTests(PostgresTestContainer postgresContainer)
        {
            var builder = postgresContainer.RegisterTestPersistenceProvider();
            container = builder.Build();

            var testPersistenceProvider = container.Resolve<IPersistenceProvider>();
            Assert.Same(postgresContainer.postgreSQLProvider, testPersistenceProvider);

            clearTables = container.Resolve<ClearAllTables>();
            var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority);

            foreach (var value in defaultValues)
            {
                value.InitAsync().Wait();
            }

            userService = container.Resolve<IUserService>();
            friendsService = container.Resolve<IFriendsService>();
            chatConversationsService = container.Resolve<IChatConversationsService>();
            messageService = container.Resolve<IChatMessageService>();
            friendRequestRepository = container.Resolve<IFriendRequestRepository>();
            friendRelationRepository = container.Resolve<IFriendRelationRepository>();
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

        #region Send

        [Fact]
        public async Task SendFriendRequest_OK()
        {
            var ct = TestContext.Current.CancellationToken;
            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(username1, ct);

            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);

            var friendRequest = await friendsService.SendFriendRequestAsync(new SendFriendRequest
            {
                Username = username2
            }, userId, ct);

            friendRequest.Should().NotBeNull();
            friendRequest.Id.Should().NotBeEmpty();
            friendRequest.InvitedById.Should().Be(userId);
            friendRequest.UserId.Should().Be(userId2);
            friendRequest.Status.Should().Be(InvitationStatus.Pending);

            await ValidateFriendRequestPendingAsync(friendRequest, userId, userId2, ct);

            // Sent list of user 1
            var sent = await friendsService.GetSentFriendRequestsAsync(new PageRequest(), userId, ct);
            sent.Items.Should().ContainSingle();
            sent.Items[0].Id.Should().Be(friendRequest.Id);
            sent.Items[0].Username.Should().Be(username2);
            (await friendsService.GetReceivedFriendRequestsAsync(new PageRequest(), userId, ct)).Items.Should().BeEmpty();

            // Received list of user 2
            var received = await friendsService.GetReceivedFriendRequestsAsync(new PageRequest(), userId2, ct);
            received.Items.Should().ContainSingle();
            received.Items[0].Id.Should().Be(friendRequest.Id);
            received.Items[0].InvitedById.Should().Be(userId);
            received.Items[0].InvitedByUsername.Should().Be(username1);
            (await friendsService.GetSentFriendRequestsAsync(new PageRequest(), userId2, ct)).Items.Should().BeEmpty();
        }

        [Fact]
        public async Task SendFriendRequest_UserNotFound_Invalid()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);

            await Assert.ThrowsAsync<ValidationException>(async () => await friendsService.SendFriendRequestAsync(new SendFriendRequest
            {
                Username = "Does not exist"
            }, userId, ct));

            (await friendsService.GetSentFriendRequestsAsync(new PageRequest(), userId, ct)).Items.Should().BeEmpty();
        }

        [Fact]
        public async Task SendFriendRequest_ToYourself_Invalid()
        {
            var ct = TestContext.Current.CancellationToken;
            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(username1, ct);

            await Assert.ThrowsAsync<ValidationException>(async () => await friendsService.SendFriendRequestAsync(new SendFriendRequest
            {
                Username = username1
            }, userId, ct));

            (await friendsService.GetSentFriendRequestsAsync(new PageRequest(), userId, ct)).Items.Should().BeEmpty();
            (await friendsService.GetReceivedFriendRequestsAsync(new PageRequest(), userId, ct)).Items.Should().BeEmpty();
        }

        [Fact]
        public async Task SendFriendRequest_Twice_Invalid()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);

            var friendRequest = await SendFriendRequestAsync(userId, username2, ct);

            // Sends again the same request
            await Assert.ThrowsAsync<ValidationException>(async () => await friendsService.SendFriendRequestAsync(new SendFriendRequest
            {
                Username = username2
            }, userId, ct));

            await ValidateFriendRequestPendingAsync(friendRequest, userId, userId2, ct);
            (await friendsService.GetSentFriendRequestsAsync(new PageRequest(), userId, ct)).Items.Should().ContainSingle();
        }

        [Fact]
        public async Task SendFriendRequest_AlreadyFriends_Invalid()
        {
            var ct = TestContext.Current.CancellationToken;
            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(username1, ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var friendRequest = await SendFriendRequestAsync(userId, username2, ct);
            await AcceptFriendRequestAsync(friendRequest.Id, userId2, ct);

            // Neither of them can send a new request
            await Assert.ThrowsAsync<ValidationException>(async () => await friendsService.SendFriendRequestAsync(new SendFriendRequest
            {
                Username = username2
            }, userId, ct));
            await Assert.ThrowsAsync<ValidationException>(async () => await friendsService.SendFriendRequestAsync(new SendFriendRequest
            {
                Username = username1
            }, userId2, ct));

            (await friendsService.GetSentFriendRequestsAsync(new PageRequest(), userId, ct)).Items.Should().BeEmpty();
            (await friendsService.GetSentFriendRequestsAsync(new PageRequest(), userId2, ct)).Items.Should().BeEmpty();
            await ValidateFriendsAsync(userId, userId2, ct);
        }

        [Fact]
        public async Task SendFriendRequest_OtherUserAlreadySentOne_AcceptsItAutomatically()
        {
            var ct = TestContext.Current.CancellationToken;
            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(username1, ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);

            // User 1 sends a request to user 2
            var friendRequest = await SendFriendRequestAsync(userId, username2, ct);

            // User 2 sends a request to user 1 instead of accepting it
            var result = await friendsService.SendFriendRequestAsync(new SendFriendRequest
            {
                Username = username1
            }, userId2, ct);

            // The request of user 1 is accepted and no new request is created
            result.Should().NotBeNull();
            result.Id.Should().Be(friendRequest.Id);
            result.InvitedById.Should().Be(userId);
            result.UserId.Should().Be(userId2);
            result.Status.Should().Be(InvitationStatus.Accepted);

            await ValidateFriendRequestAcceptedAsync(friendRequest, userId, userId2, ct);
            await ValidateFriendsAsync(userId, userId2, ct);
            (await friendsService.GetSentFriendRequestsAsync(new PageRequest(), userId2, ct)).Items.Should().BeEmpty();
            (await friendsService.GetReceivedFriendRequestsAsync(new PageRequest(), userId, ct)).Items.Should().BeEmpty();
        }

        [Fact]
        public async Task SendFriendRequest_ManyUsers_EachOneReceivesItsRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var receivers = new List<UserDto>();
            for (int i = 0; i < 5; i++)
            {
                receivers.Add(await CreateUserDtoAsync($"Receiver {i}", ct));
            }

            foreach (var receiver in receivers)
            {
                await SendFriendRequestAsync(userId, receiver.Username!, ct);
            }

            var sent = await friendsService.GetSentFriendRequestsAsync(new PageRequest(), userId, ct);
            sent.Items.Should().HaveCount(receivers.Count);
            sent.Total.Should().Be(receivers.Count);
            sent.Items.Select(x => x.UserId).Should().BeEquivalentTo(receivers.Select(x => x.Id));

            foreach (var receiver in receivers)
            {
                var received = await friendsService.GetReceivedFriendRequestsAsync(new PageRequest(), receiver.Id, ct);
                received.Items.Should().ContainSingle().Which.InvitedById.Should().Be(userId);
            }
        }

        [Fact]
        public async Task SendFriendRequest_NullRequest_Throws()
        {
            var ct = TestContext.Current.CancellationToken;

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await friendsService.SendFriendRequestAsync(null!, Guid.NewGuid(), ct));
        }

        #endregion

        #region Accept

        [Fact]
        public async Task AcceptFriendRequest_OK()
        {
            var ct = TestContext.Current.CancellationToken;
            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(username1, ct);

            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);

            var friendRequest = await friendsService.SendFriendRequestAsync(new SendFriendRequest
            {
                Username = username2
            }, userId, ct);

            friendRequest.Should().NotBeNull();

            var relationId = await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = friendRequest.Id,
                Status = InvitationStatus.Accepted
            }, userId2, ct);

            relationId.Should().NotBeNull().And.NotBeEmpty();
            await ValidateFriendRequestAcceptedAsync(friendRequest, userId, userId2, ct);

            // Friends of user 1
            var friends1 = await friendsService.GetUserFriendsAsync(new PageRequest(), userId, ct);
            friends1.Items.Should().HaveCount(1);
            var first = friends1.Items[0];
            first.FriendRelationId.Should().Be(relationId!.Value);
            first.Friend.Should().NotBeNull();
            first.Friend.Id.Should().Be(userId2);
            first.Friend.Username.Should().Be(username2);
            first.ChatConversationId.Should().NotBeEmpty();

            await friendsService.ValidateUserCanSendMessageAsync(first.ChatConversationId, userId, ct);

            // Friends of user 2
            var friends2 = await friendsService.GetUserFriendsAsync(new PageRequest(), userId2, ct);
            friends2.Items.Should().HaveCount(1);
            var second = friends2.Items[0];
            second.FriendRelationId.Should().Be(relationId.Value);
            second.Friend.Should().NotBeNull();
            second.Friend.Id.Should().Be(userId);
            second.Friend.Username.Should().Be(username1);
            // Both users share the same direct conversation
            second.ChatConversationId.Should().Be(first.ChatConversationId);

            await friendsService.ValidateUserCanSendMessageAsync(second.ChatConversationId, userId2, ct);

            // The conversation is a direct one
            var conversation = await chatConversationsService.GetChatConversationByIdAsync(first.ChatConversationId, ct);
            conversation.Should().NotBeNull();
            conversation!.ConversationType.Should().Be(ChatConversationType.Direct);

            // The request is no longer pending
            (await friendsService.GetSentFriendRequestsAsync(new PageRequest(), userId, ct)).Items.Should().BeEmpty();
            (await friendsService.GetReceivedFriendRequestsAsync(new PageRequest(), userId2, ct)).Items.Should().BeEmpty();
        }

        [Fact]
        public async Task AcceptFriendRequest_SenderTriesToAccept_Invalid()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var friendRequest = await SendFriendRequestAsync(userId, username2, ct);

            // The sender accepts its own request
            await Assert.ThrowsAsync<ValidationException>(async () => await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = friendRequest.Id,
                Status = InvitationStatus.Accepted
            }, userId, ct));

            await ValidateFriendRequestPendingAsync(friendRequest, userId, userId2, ct);

            // User 2 can still accept it
            await AcceptFriendRequestAsync(friendRequest.Id, userId2, ct);
            await ValidateFriendRequestAcceptedAsync(friendRequest, userId, userId2, ct);
        }

        [Fact]
        public async Task AcceptFriendRequest_User3TriesToAcceptAnotherRequest_Invalid()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var username3 = "Username 3";
            Guid userId3 = await CreateUserAsync(username3, ct);

            var friendRequest2 = await SendFriendRequestAsync(userId, username2, ct);
            var friendRequest3 = await SendFriendRequestAsync(userId, username3, ct);

            // User 3 tries to accept the request of user 2
            await Assert.ThrowsAsync<ValidationException>(async () => await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = friendRequest2.Id,
                Status = InvitationStatus.Accepted
            }, userId3, ct));

            await ValidateFriendRequestPendingAsync(friendRequest2, userId, userId2, ct);
            await ValidateFriendRequestPendingAsync(friendRequest3, userId, userId3, ct);

            // Each user accepts its own request
            await AcceptFriendRequestAsync(friendRequest2.Id, userId2, ct);
            await AcceptFriendRequestAsync(friendRequest3.Id, userId3, ct);

            await ValidateFriendRequestAcceptedAsync(friendRequest2, userId, userId2, ct);
            await ValidateFriendRequestAcceptedAsync(friendRequest3, userId, userId3, ct);
            (await friendsService.GetUserFriendsAsync(new PageRequest(), userId, ct)).Items.Select(x => x.Friend.Id)
                .Should().BeEquivalentTo([userId2, userId3]);
            // User 2 and user 3 are not friends between them
            (await friendRelationRepository.AreFriendsAsync(userId2, userId3, ct)).Should().BeFalse();
        }

        [Fact]
        public async Task AcceptFriendRequest_Twice_Invalid()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var friendRequest = await SendFriendRequestAsync(userId, username2, ct);
            var relationId = await AcceptFriendRequestAsync(friendRequest.Id, userId2, ct);

            await Assert.ThrowsAsync<ValidationException>(async () => await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = friendRequest.Id,
                Status = InvitationStatus.Accepted
            }, userId2, ct));

            var friends = await friendsService.GetUserFriendsAsync(new PageRequest(), userId, ct);
            friends.Items.Should().ContainSingle().Which.FriendRelationId.Should().Be(relationId);
        }

        [Fact]
        public async Task AcceptFriendRequest_NotExisting_Invalid()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);

            await Assert.ThrowsAsync<ValidationException>(async () => await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = Guid.NewGuid(),
                Status = InvitationStatus.Accepted
            }, userId, ct));
        }

        [Fact]
        public async Task UpdateFriendRequest_PendingStatus_Invalid()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var friendRequest = await SendFriendRequestAsync(userId, username2, ct);

            await Assert.ThrowsAnyAsync<ValidationException>(async () => await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = friendRequest.Id,
                Status = InvitationStatus.Pending
            }, userId2, ct));

            await ValidateFriendRequestPendingAsync(friendRequest, userId, userId2, ct);
        }

        [Fact]
        public async Task UpdateFriendRequest_NullRequest_Throws()
        {
            var ct = TestContext.Current.CancellationToken;

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await friendsService.UpdateFriendRequestAsync(null!, Guid.NewGuid(), ct));
        }

        #endregion

        #region Reject

        [Fact]
        public async Task RejectFriendRequest_OK()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var friendRequest = await SendFriendRequestAsync(userId, username2, ct);

            var result = await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = friendRequest.Id,
                Status = InvitationStatus.Rejected
            }, userId2, ct);

            result.Should().BeNull();
            await ValidateFriendRequestRejectedAsync(friendRequest, userId, userId2, ct);
            await ValidateNotFriendsAsync(userId, userId2, ct);
            (await friendsService.GetSentFriendRequestsAsync(new PageRequest(), userId, ct)).Items.Should().BeEmpty();
            (await friendsService.GetReceivedFriendRequestsAsync(new PageRequest(), userId2, ct)).Items.Should().BeEmpty();
        }

        [Fact]
        public async Task RejectFriendRequest_SenderTriesToReject_Invalid()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var friendRequest = await SendFriendRequestAsync(userId, username2, ct);

            // The sender has to revoke it, not reject it
            await Assert.ThrowsAsync<ValidationException>(async () => await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = friendRequest.Id,
                Status = InvitationStatus.Rejected
            }, userId, ct));

            await ValidateFriendRequestPendingAsync(friendRequest, userId, userId2, ct);
        }

        [Fact]
        public async Task RejectFriendRequest_User3TriesToReject_Invalid()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            Guid userId3 = await CreateUserAsync("Username 3", ct);
            var friendRequest = await SendFriendRequestAsync(userId, username2, ct);

            await Assert.ThrowsAsync<ValidationException>(async () => await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = friendRequest.Id,
                Status = InvitationStatus.Rejected
            }, userId3, ct));

            await ValidateFriendRequestPendingAsync(friendRequest, userId, userId2, ct);
        }

        [Fact]
        public async Task RejectFriendRequest_AcceptedRequest_Invalid()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var friendRequest = await SendFriendRequestAsync(userId, username2, ct);
            await AcceptFriendRequestAsync(friendRequest.Id, userId2, ct);

            await Assert.ThrowsAsync<ValidationException>(async () => await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = friendRequest.Id,
                Status = InvitationStatus.Rejected
            }, userId2, ct));

            // Still accepted and still friends
            await ValidateFriendRequestAcceptedAsync(friendRequest, userId, userId2, ct);
            await ValidateFriendsAsync(userId, userId2, ct);
        }

        [Fact]
        public async Task AcceptFriendRequest_RejectedRequest_Invalid()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var friendRequest = await SendFriendRequestAsync(userId, username2, ct);
            await RejectFriendRequestAsync(friendRequest.Id, userId2, ct);

            await Assert.ThrowsAsync<ValidationException>(async () => await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = friendRequest.Id,
                Status = InvitationStatus.Accepted
            }, userId2, ct));

            await ValidateFriendRequestRejectedAsync(friendRequest, userId, userId2, ct);
            await ValidateNotFriendsAsync(userId, userId2, ct);
        }

        [Fact]
        public async Task RejectFriendRequest_SentAgain_AndAccepts()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var friendRequest = await SendFriendRequestAsync(userId, username2, ct);
            await RejectFriendRequestAsync(friendRequest.Id, userId2, ct);

            // User 1 sends a new request
            var friendRequest2 = await SendFriendRequestAsync(userId, username2, ct);
            friendRequest2.Id.Should().NotBe(friendRequest.Id);
            await ValidateFriendRequestPendingAsync(friendRequest2, userId, userId2, ct);

            // User 2 accepts the second one
            await AcceptFriendRequestAsync(friendRequest2.Id, userId2, ct);

            await ValidateFriendRequestAcceptedAsync(friendRequest2, userId, userId2, ct);
            await ValidateFriendRequestRejectedAsync(friendRequest, userId, userId2, ct);
            await ValidateFriendsAsync(userId, userId2, ct);
        }

        [Fact]
        public async Task RejectFriendRequest_RejectedUserSendsRequestBack_AndIsAccepted()
        {
            var ct = TestContext.Current.CancellationToken;
            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(username1, ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var friendRequest = await SendFriendRequestAsync(userId, username2, ct);
            await RejectFriendRequestAsync(friendRequest.Id, userId2, ct);

            // User 2 changes its mind and sends its own request
            var friendRequest2 = await SendFriendRequestAsync(userId2, username1, ct);
            friendRequest2.Status.Should().Be(InvitationStatus.Pending);
            await ValidateFriendRequestPendingAsync(friendRequest2, userId2, userId, ct);

            await AcceptFriendRequestAsync(friendRequest2.Id, userId, ct);
            await ValidateFriendsAsync(userId, userId2, ct);
        }

        #endregion

        #region Revoke

        [Fact]
        public async Task RevokeFriendRequest_OK()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var friendRequest = await SendFriendRequestAsync(userId, username2, ct);

            var result = await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = friendRequest.Id,
                Status = InvitationStatus.Revoked
            }, userId, ct);

            result.Should().BeNull();
            await ValidateFriendRequestRevokedAsync(friendRequest, userId, userId2, ct);
            await ValidateNotFriendsAsync(userId, userId2, ct);
            (await friendsService.GetSentFriendRequestsAsync(new PageRequest(), userId, ct)).Items.Should().BeEmpty();
            (await friendsService.GetReceivedFriendRequestsAsync(new PageRequest(), userId2, ct)).Items.Should().BeEmpty();
        }

        [Fact]
        public async Task RevokeFriendRequest_ReceiverTriesToRevoke_Invalid()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var friendRequest = await SendFriendRequestAsync(userId, username2, ct);

            // The receiver has to reject it, not revoke it
            await Assert.ThrowsAsync<ValidationException>(async () => await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = friendRequest.Id,
                Status = InvitationStatus.Revoked
            }, userId2, ct));

            await ValidateFriendRequestPendingAsync(friendRequest, userId, userId2, ct);
        }

        [Fact]
        public async Task RevokeFriendRequest_User3TriesToRevoke_Invalid()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            Guid userId3 = await CreateUserAsync("Username 3", ct);
            var friendRequest = await SendFriendRequestAsync(userId, username2, ct);

            await Assert.ThrowsAsync<ValidationException>(async () => await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = friendRequest.Id,
                Status = InvitationStatus.Revoked
            }, userId3, ct));

            await ValidateFriendRequestPendingAsync(friendRequest, userId, userId2, ct);
        }

        [Fact]
        public async Task RevokeFriendRequest_AcceptedRequest_Invalid()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var friendRequest = await SendFriendRequestAsync(userId, username2, ct);
            await AcceptFriendRequestAsync(friendRequest.Id, userId2, ct);

            await Assert.ThrowsAsync<ValidationException>(async () => await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = friendRequest.Id,
                Status = InvitationStatus.Revoked
            }, userId, ct));

            await ValidateFriendRequestAcceptedAsync(friendRequest, userId, userId2, ct);
            await ValidateFriendsAsync(userId, userId2, ct);
        }

        [Fact]
        public async Task RevokeFriendRequest_RejectedRequest_Invalid()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var friendRequest = await SendFriendRequestAsync(userId, username2, ct);
            await RejectFriendRequestAsync(friendRequest.Id, userId2, ct);

            await Assert.ThrowsAsync<ValidationException>(async () => await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = friendRequest.Id,
                Status = InvitationStatus.Revoked
            }, userId, ct));

            await ValidateFriendRequestRejectedAsync(friendRequest, userId, userId2, ct);
        }

        [Fact]
        public async Task AcceptFriendRequest_RevokedRequest_Invalid()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var friendRequest = await SendFriendRequestAsync(userId, username2, ct);
            await RevokeFriendRequestAsync(friendRequest.Id, userId, ct);

            await Assert.ThrowsAsync<ValidationException>(async () => await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = friendRequest.Id,
                Status = InvitationStatus.Accepted
            }, userId2, ct));

            await ValidateFriendRequestRevokedAsync(friendRequest, userId, userId2, ct);
            await ValidateNotFriendsAsync(userId, userId2, ct);
        }

        [Fact]
        public async Task RevokeFriendRequest_SentAgain_AndAccepts()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var friendRequest = await SendFriendRequestAsync(userId, username2, ct);
            await RevokeFriendRequestAsync(friendRequest.Id, userId, ct);

            var friendRequest2 = await SendFriendRequestAsync(userId, username2, ct);
            friendRequest2.Id.Should().NotBe(friendRequest.Id);
            await AcceptFriendRequestAsync(friendRequest2.Id, userId2, ct);

            await ValidateFriendRequestAcceptedAsync(friendRequest2, userId, userId2, ct);
            await ValidateFriendRequestRevokedAsync(friendRequest, userId, userId2, ct);
            await ValidateFriendsAsync(userId, userId2, ct);
        }

        #endregion

        #region Friends list

        [Fact]
        public async Task GetUserFriends_Empty()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);

            var friends = await friendsService.GetUserFriendsAsync(new PageRequest(), userId, ct);

            friends.Items.Should().BeEmpty();
            friends.Total.Should().Be(0);
        }

        [Fact]
        public async Task GetUserFriends_PendingRequest_IsNotAFriend()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            await SendFriendRequestAsync(userId, username2, ct);

            await ValidateNotFriendsAsync(userId, userId2, ct);
        }

        [Fact]
        public async Task GetUserFriends_Paginated_SortedByUsername()
        {
            var ct = TestContext.Current.CancellationToken;
            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(username1, ct);

            int friendsCount = 12;
            var friends = new List<UserDto>();
            for (int i = 0; i < friendsCount; i++)
            {
                var friend = await CreateUserDtoAsync($"{i}-{Guid.NewGuid()}", ct);
                friends.Add(friend);

                // Half of the requests are sent by the user and half are received
                if (i % 2 == 0)
                {
                    var request = await SendFriendRequestAsync(userId, friend.Username!, ct);
                    await AcceptFriendRequestAsync(request.Id, friend.Id, ct);
                }
                else
                {
                    var request = await SendFriendRequestAsync(friend.Id, username1, ct);
                    await AcceptFriendRequestAsync(request.Id, userId, ct);
                }
            }

            var sortedUsernames = friends.Select(x => x.Username).OrderBy(x => x!, StringComparer.InvariantCultureIgnoreCase).ToList();

            int pageSize = 5;
            Guid? next = null;
            var pagedFriends = new List<UserFriendResponse>();
            do
            {
                var page = await friendsService.GetUserFriendsAsync(new PageRequest
                {
                    Next = next,
                    Descending = false,
                    PageSize = pageSize,
                    SortBy = "username"
                }, userId, ct);

                if (page.Next != null)
                {
                    page.Items.Count.Should().Be(pageSize);
                    page.PageSize.Should().Be(pageSize);
                }
                page.Total.Should().Be(friendsCount);

                pagedFriends.AddRange(page.Items);
                next = page.Next;

            } while (next != null);

            pagedFriends.Should().HaveCount(friendsCount);
            pagedFriends.Select(x => x.Friend.Username).ToList().Should().ContainInConsecutiveOrder(sortedUsernames);
            pagedFriends.Select(x => x.ChatConversationId).Should().OnlyHaveUniqueItems();
        }

        [Fact]
        public async Task GetUserFriends_NullRequest_Throws()
        {
            var ct = TestContext.Current.CancellationToken;

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await friendsService.GetUserFriendsAsync(null!, Guid.NewGuid(), ct));
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await friendsService.GetSentFriendRequestsAsync(null!, Guid.NewGuid(), ct));
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await friendsService.GetReceivedFriendRequestsAsync(null!, Guid.NewGuid(), ct));
        }

        #endregion

        #region Direct messages

        [Fact]
        public async Task DirectMessages_Friends_CanSendAndReadMessages()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var conversationId = await MakeFriendsAsync(userId, userId2, username2, ct);

            var message = await messageService.CreateChatMessageAsync(new CreateChatMessageRequest
            {
                AuthorId = userId,
                ChatConversationId = conversationId,
                Content = "Hello"
            }, ct);

            message.Should().NotBeNull();
            message.ChatConversationId.Should().Be(conversationId);

            var message2 = await messageService.CreateChatMessageAsync(new CreateChatMessageRequest
            {
                AuthorId = userId2,
                ChatConversationId = conversationId,
                Content = "Hello 2"
            }, ct);

            message2.Should().NotBeNull();
            message2.ChatConversationId.Should().Be(conversationId);

            var messages = await messageService.GetMessagesByConversationIdAsync(conversationId, ct: ct);

            messages.Items.Should().HaveCount(2);
            // Newest first
            messages.Items.Select(x => x.Id).Should().Equal(message2.Id, message.Id);
            messages.Items.Select(x => x.Author!.Id).Should().Equal(userId2, userId);
        }

        [Fact]
        public async Task DirectMessages_UserOutsideTheConversation_CannotSendMessages()
        {
            var ct = TestContext.Current.CancellationToken;
            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(username1, ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var username3 = "Username 3";
            Guid userId3 = await CreateUserAsync(username3, ct);
            var conversationId = await MakeFriendsAsync(userId, userId2, username2, ct);
            // User 3 is friends with both of them, but the conversation is not theirs
            await MakeFriendsAsync(userId3, userId, username1, ct);
            await MakeFriendsAsync(userId3, userId2, username2, ct);

            await Assert.ThrowsAsync<ValidationException>(async () => await friendsService.ValidateUserCanSendMessageAsync(conversationId, userId3, ct));
            await Assert.ThrowsAsync<ValidationException>(async () => await messageService.CreateChatMessageAsync(new CreateChatMessageRequest
            {
                AuthorId = userId3,
                ChatConversationId = conversationId,
                Content = "Intruder"
            }, ct));

            (await messageService.GetMessagesByConversationIdAsync(conversationId, ct: ct)).Items.Should().BeEmpty();
        }

        [Fact]
        public async Task DirectMessages_NotFriends_HaveNoConversation()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var username3 = "Username 3";
            Guid userId3 = await CreateUserAsync(username3, ct);
            // Pending, rejected and revoked requests do not create a conversation
            await SendFriendRequestAsync(userId, username2, ct);
            var rejected = await SendFriendRequestAsync(userId, username3, ct);
            await RejectFriendRequestAsync(rejected.Id, userId3, ct);

            (await friendsService.GetUserFriendsAsync(new PageRequest(), userId, ct)).Items.Should().BeEmpty();
            (await friendsService.GetUserFriendsAsync(new PageRequest(), userId2, ct)).Items.Should().BeEmpty();
            (await friendsService.GetUserFriendsAsync(new PageRequest(), userId3, ct)).Items.Should().BeEmpty();
        }

        [Fact]
        public async Task DirectMessages_NotExistingConversation_CannotSendMessages()
        {
            var ct = TestContext.Current.CancellationToken;
            Guid userId = await CreateUserAsync("Username 1", ct);
            var conversationId = Guid.NewGuid();

            await Assert.ThrowsAsync<ValidationException>(async () => await friendsService.ValidateUserCanSendMessageAsync(conversationId, userId, ct));
            await Assert.ThrowsAsync<ValidationException>(async () => await messageService.CreateChatMessageAsync(new CreateChatMessageRequest
            {
                AuthorId = userId,
                ChatConversationId = conversationId,
                Content = "Hello"
            }, ct));
        }

        [Fact]
        public async Task DirectMessages_EachPairHasItsOwnConversation()
        {
            var ct = TestContext.Current.CancellationToken;
            var username1 = "Username 1";
            Guid userId = await CreateUserAsync(username1, ct);
            var username2 = "Username 2";
            Guid userId2 = await CreateUserAsync(username2, ct);
            var username3 = "Username 3";
            Guid userId3 = await CreateUserAsync(username3, ct);
            var conversation12 = await MakeFriendsAsync(userId, userId2, username2, ct);
            var conversation13 = await MakeFriendsAsync(userId, userId3, username3, ct);

            conversation12.Should().NotBe(conversation13);

            await messageService.CreateChatMessageAsync(new CreateChatMessageRequest { AuthorId = userId, ChatConversationId = conversation12, Content = "To 2" }, ct);
            await messageService.CreateChatMessageAsync(new CreateChatMessageRequest { AuthorId = userId3, ChatConversationId = conversation13, Content = "To 1" }, ct);

            (await messageService.GetMessagesByConversationIdAsync(conversation12, ct: ct)).Items.Select(x => x.Content).Should().Equal("To 2");
            (await messageService.GetMessagesByConversationIdAsync(conversation13, ct: ct)).Items.Select(x => x.Content).Should().Equal("To 1");

            // User 2 cannot write in the conversation between user 1 and user 3
            await Assert.ThrowsAsync<ValidationException>(async () => await messageService.CreateChatMessageAsync(new CreateChatMessageRequest
            {
                AuthorId = userId2,
                ChatConversationId = conversation13,
                Content = "Intruder"
            }, ct));
        }

        #endregion

        private async Task<FriendRequestDto> SendFriendRequestAsync(Guid userId, string username, CancellationToken ct)
        {
            return await friendsService.SendFriendRequestAsync(new SendFriendRequest
            {
                Username = username
            }, userId, ct);
        }

        private async Task<Guid> AcceptFriendRequestAsync(Guid friendRequestId, Guid userId, CancellationToken ct)
        {
            var relationId = await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = friendRequestId,
                Status = InvitationStatus.Accepted
            }, userId, ct);

            relationId.Should().NotBeNull();
            return relationId!.Value;
        }

        private async Task RejectFriendRequestAsync(Guid friendRequestId, Guid userId, CancellationToken ct)
        {
            await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = friendRequestId,
                Status = InvitationStatus.Rejected
            }, userId, ct);
        }

        private async Task RevokeFriendRequestAsync(Guid friendRequestId, Guid userId, CancellationToken ct)
        {
            await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = friendRequestId,
                Status = InvitationStatus.Revoked
            }, userId, ct);
        }

        /// <summary>
        /// The first user sends the request and the second one accepts it. Returns their direct conversation.
        /// </summary>
        private async Task<Guid> MakeFriendsAsync(Guid userId, Guid friendId, string friendUsername, CancellationToken ct)
        {
            var request = await SendFriendRequestAsync(userId, friendUsername, ct);
            await AcceptFriendRequestAsync(request.Id, friendId, ct);

            var friends = await friendsService.GetUserFriendsAsync(new PageRequest { PageSize = 50 }, userId, ct);
            return friends.Items.Single(x => x.Friend.Id == friendId).ChatConversationId;
        }

        private async Task ValidateFriendsAsync(Guid userId, Guid userId2, CancellationToken ct)
        {
            (await friendRelationRepository.AreFriendsAsync(userId, userId2, ct)).Should().BeTrue();

            var friends1 = await friendsService.GetUserFriendsAsync(new PageRequest { PageSize = 50 }, userId, ct);
            var friends2 = await friendsService.GetUserFriendsAsync(new PageRequest { PageSize = 50 }, userId2, ct);
            var relation1 = friends1.Items.Should().ContainSingle(x => x.Friend.Id == userId2).Subject;
            var relation2 = friends2.Items.Should().ContainSingle(x => x.Friend.Id == userId).Subject;
            relation1.FriendRelationId.Should().Be(relation2.FriendRelationId);
            relation1.ChatConversationId.Should().Be(relation2.ChatConversationId);

            // Both of them can write in their direct conversation
            await friendsService.ValidateUserCanSendMessageAsync(relation1.ChatConversationId, userId, ct);
            await friendsService.ValidateUserCanSendMessageAsync(relation1.ChatConversationId, userId2, ct);
        }

        private async Task ValidateNotFriendsAsync(Guid userId, Guid userId2, CancellationToken ct)
        {
            (await friendRelationRepository.AreFriendsAsync(userId, userId2, ct)).Should().BeFalse();

            var friends1 = await friendsService.GetUserFriendsAsync(new PageRequest { PageSize = 50 }, userId, ct);
            var friends2 = await friendsService.GetUserFriendsAsync(new PageRequest { PageSize = 50 }, userId2, ct);
            friends1.Items.Should().NotContain(x => x.Friend.Id == userId2);
            friends2.Items.Should().NotContain(x => x.Friend.Id == userId);
        }

        private Task ValidateFriendRequestPendingAsync(FriendRequestDto friendRequest, Guid invitedBy, Guid userId, CancellationToken ct)
            => ValidateFriendRequestAsync(friendRequest, invitedBy, userId, InvitationStatus.Pending, ct);

        private Task ValidateFriendRequestAcceptedAsync(FriendRequestDto friendRequest, Guid invitedBy, Guid userId, CancellationToken ct)
            => ValidateFriendRequestAsync(friendRequest, invitedBy, userId, InvitationStatus.Accepted, ct);

        private Task ValidateFriendRequestRejectedAsync(FriendRequestDto friendRequest, Guid invitedBy, Guid userId, CancellationToken ct)
            => ValidateFriendRequestAsync(friendRequest, invitedBy, userId, InvitationStatus.Rejected, ct);

        private Task ValidateFriendRequestRevokedAsync(FriendRequestDto friendRequest, Guid invitedBy, Guid userId, CancellationToken ct)
            => ValidateFriendRequestAsync(friendRequest, invitedBy, userId, InvitationStatus.Revoked, ct);

        private async Task ValidateFriendRequestAsync(FriendRequestDto friendRequest, Guid invitedBy, Guid userId, InvitationStatus status, CancellationToken ct)
        {
            var dbRequest = await friendRequestRepository.GetFriendRequestByIdAsync(friendRequest.Id, ct);
            dbRequest.Should().NotBeNull();
            dbRequest!.Id.Should().Be(friendRequest.Id);
            dbRequest.InvitedBy.Should().Be(invitedBy);
            dbRequest.UserId.Should().Be(userId);
            dbRequest.UserId.Should().Be(friendRequest.UserId);
            dbRequest.Status.Should().Be(status);
        }

        private async Task<UserDto> CreateUserDtoAsync(string? username, CancellationToken ct)
        {
            var random = Guid.NewGuid().ToString();
            return (await userService.CreateUserAsync(new CreateUserRequest
            {
                Email = random,
                Username = username ?? random,
                Password = random
            }));
        }

        private async Task<Guid> CreateUserAsync(string? username, CancellationToken ct)
        {
            return (await CreateUserDtoAsync(username, ct)).Id;
        }
    }
}
