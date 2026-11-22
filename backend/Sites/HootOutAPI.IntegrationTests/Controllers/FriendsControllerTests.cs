using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Enums;
using HootOut.Contracts.Friends.Dtos;
using HootOut.Contracts.Friends.Requests;
using HootOut.Contracts.Friends.Responses;
using HootOut.HootOutAPI.IntegrationTests.Common;
using System.Net;
using System.Net.Http.Json;

namespace HootOut.HootOutAPI.IntegrationTests.Controllers
{
    [Collection("IntegrationTests")]
    public class FriendsControllerTest : TestControllerBase, IClassFixture<APIFixture>, IDisposable, IAsyncLifetime
    {
        private const string FriendsUrl = "/api/v1/friends";
        private const string RequestsUrl = "/api/v1/friends/requests";
        private const string SentRequestsUrl = "/api/v1/friends/requests/sent";
        private const string ReceivedRequestsUrl = "/api/v1/friends/requests/received";

        private readonly ILifetimeScope container;
        private readonly ClearAllTables clearTables;

        public FriendsControllerTest(APIFixture apiFixture)
        {
            httpClient = apiFixture.CreateHttpsClient();
            container = apiFixture.AutofacRoot.BeginLifetimeScope();

            var testPersistenceProvider = container.Resolve<IPersistenceProvider>();
            Assert.Same(apiFixture.PostgreSQLProvider, testPersistenceProvider);

            clearTables = container.Resolve<ClearAllTables>();
            var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority);

            foreach (var value in defaultValues)
            {
                value.InitAsync().Wait();
            }
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

        #region Authorization

        [Theory]
        [InlineData("GET", FriendsUrl)]
        [InlineData("GET", SentRequestsUrl)]
        [InlineData("GET", ReceivedRequestsUrl)]
        [InlineData("POST", RequestsUrl)]
        [InlineData("PUT", RequestsUrl)]
        public async Task Endpoints_WithoutToken_ReturnUnauthorized(string method, string url)
        {
            var ct = TestContext.Current.CancellationToken;

            // A valid body is sent in POST and PUT so the request is not rejected by the model validation before the authorization check
            using var request = new HttpRequestMessage(new HttpMethod(method), url);
            request.Content = method switch
            {
                "POST" => JsonContent.Create(new SendFriendRequest { Username = "user-test-b" }),
                "PUT" => JsonContent.Create(new UpdateFriendRequest { Id = Guid.NewGuid(), Status = InvitationStatus.Accepted }),
                _ => null
            };

            var response = await httpClient!.SendAsync(request, ct);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        #endregion

        #region Send

        [Fact]
        public async Task SendFriendRequest_ReturnsOk_AppearsInSentAndReceived()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);

            var response = await SendFriendRequestAsync(userA, userB.Username, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var friendRequest = await response.Content.ReadFromJsonAsync<FriendRequestDto>(ct);
            friendRequest.Should().NotBeNull();
            friendRequest!.Id.Should().NotBeEmpty();
            friendRequest.InvitedById.Should().Be(userA.Id);
            friendRequest.UserId.Should().Be(userB.Id);
            friendRequest.Status.Should().Be(InvitationStatus.Pending);

            var sent = await GetPageAsync<FriendRequestDto>(SentRequestsUrl, userA, ct);
            var sentRequest = sent.Items.Should().ContainSingle().Subject;
            sentRequest.Id.Should().Be(friendRequest.Id);
            sentRequest.UserId.Should().Be(userB.Id);
            sentRequest.Username.Should().Be(userB.Username);
            sentRequest.Status.Should().Be(InvitationStatus.Pending);

            var received = await GetPageAsync<FriendRequestDto>(ReceivedRequestsUrl, userB, ct);
            var receivedRequest = received.Items.Should().ContainSingle().Subject;
            receivedRequest.Id.Should().Be(friendRequest.Id);
            receivedRequest.InvitedById.Should().Be(userA.Id);
            receivedRequest.InvitedByUsername.Should().Be(userA.Username);

            // Each user only sees its own side of the request
            (await GetPageAsync<FriendRequestDto>(ReceivedRequestsUrl, userA, ct)).Items.Should().BeEmpty();
            (await GetPageAsync<FriendRequestDto>(SentRequestsUrl, userB, ct)).Items.Should().BeEmpty();
        }

        [Fact]
        public async Task SendFriendRequest_UnknownUser_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);

            var response = await SendFriendRequestAsync(userA, "user-test-missing", ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await GetPageAsync<FriendRequestDto>(SentRequestsUrl, userA, ct)).Items.Should().BeEmpty();
        }

        [Theory]
        [InlineData("")]
        [InlineData("foo")]
        public async Task SendFriendRequest_InvalidUsername_ReturnsBadRequest(string username)
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);

            var response = await SendFriendRequestAsync(userA, username, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await GetPageAsync<FriendRequestDto>(SentRequestsUrl, userA, ct)).Items.Should().BeEmpty();
        }

        [Fact]
        public async Task SendFriendRequest_ToYourself_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);

            var response = await SendFriendRequestAsync(userA, userA.Username, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await GetPageAsync<FriendRequestDto>(SentRequestsUrl, userA, ct)).Items.Should().BeEmpty();
            (await GetPageAsync<FriendRequestDto>(ReceivedRequestsUrl, userA, ct)).Items.Should().BeEmpty();
        }

        [Fact]
        public async Task SendFriendRequest_Twice_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var friendRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);

            var response = await SendFriendRequestAsync(userA, userB.Username, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await GetPageAsync<FriendRequestDto>(SentRequestsUrl, userA, ct)).Items
                .Should().ContainSingle().Which.Id.Should().Be(friendRequest.Id);
            (await GetPageAsync<FriendRequestDto>(ReceivedRequestsUrl, userB, ct)).Items
                .Should().ContainSingle().Which.Id.Should().Be(friendRequest.Id);
        }

        [Fact]
        public async Task SendFriendRequest_AlreadyFriends_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            await MakeFriendsAsync(userA, userB, ct);

            (await SendFriendRequestAsync(userA, userB.Username, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await SendFriendRequestAsync(userB, userA.Username, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

            (await GetPageAsync<FriendRequestDto>(SentRequestsUrl, userA, ct)).Items.Should().BeEmpty();
            (await GetPageAsync<FriendRequestDto>(SentRequestsUrl, userB, ct)).Items.Should().BeEmpty();
            await AssertFriendsAsync(userA, userB, ct);
        }

        [Fact]
        public async Task SendFriendRequest_OtherUserAlreadySentOne_ReturnsAcceptedRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var friendRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);

            var response = await SendFriendRequestAsync(userB, userA.Username, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<FriendRequestDto>(ct);
            result.Should().NotBeNull();
            result!.Id.Should().Be(friendRequest.Id);
            result.InvitedById.Should().Be(userA.Id);
            result.UserId.Should().Be(userB.Id);
            result.Status.Should().Be(InvitationStatus.Accepted);

            await AssertFriendsAsync(userA, userB, ct);
            (await GetPageAsync<FriendRequestDto>(SentRequestsUrl, userA, ct)).Items.Should().BeEmpty();
            (await GetPageAsync<FriendRequestDto>(SentRequestsUrl, userB, ct)).Items.Should().BeEmpty();
            (await GetPageAsync<FriendRequestDto>(ReceivedRequestsUrl, userA, ct)).Items.Should().BeEmpty();
            (await GetPageAsync<FriendRequestDto>(ReceivedRequestsUrl, userB, ct)).Items.Should().BeEmpty();
        }

        [Fact]
        public async Task SendFriendRequest_ManyUsers_EachOneReceivesItsRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var receivers = new List<TestUser>();
            for (int i = 0; i < 3; i++)
            {
                receivers.Add(await CreateUserAsync($"user-test-{i}", ct));
            }

            foreach (var receiver in receivers)
            {
                await SendFriendRequestAndReadAsync(userA, receiver.Username, ct);
            }

            var sent = await GetPageAsync<FriendRequestDto>(SentRequestsUrl, userA, ct);
            sent.Total.Should().Be(receivers.Count);
            sent.Items.Select(x => x.UserId).Should().BeEquivalentTo(receivers.Select(x => x.Id));

            foreach (var receiver in receivers)
            {
                var received = await GetPageAsync<FriendRequestDto>(ReceivedRequestsUrl, receiver, ct);
                received.Items.Should().ContainSingle().Which.InvitedById.Should().Be(userA.Id);
            }
        }

        #endregion

        #region Accept

        [Fact]
        public async Task AcceptFriendRequest_ReturnsOk_BothAreFriendsWithTheSameConversation()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var friendRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);

            var response = await UpdateFriendRequestAsync(userB, friendRequest.Id, InvitationStatus.Accepted, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var relationId = await response.Content.ReadFromJsonAsync<Guid?>(ct);
            relationId.Should().NotBeNull().And.NotBeEmpty();

            var friendsA = await GetPageAsync<UserFriendResponse>(FriendsUrl, userA, ct);
            var relationA = friendsA.Items.Should().ContainSingle().Subject;
            relationA.FriendRelationId.Should().Be(relationId!.Value);
            relationA.Friend.Should().NotBeNull();
            relationA.Friend.Id.Should().Be(userB.Id);
            relationA.Friend.Username.Should().Be(userB.Username);
            relationA.ChatConversationId.Should().NotBeEmpty();
            relationA.DirectConversationId.Should().NotBeEmpty().And.NotBe(relationA.ChatConversationId);

            var friendsB = await GetPageAsync<UserFriendResponse>(FriendsUrl, userB, ct);
            var relationB = friendsB.Items.Should().ContainSingle().Subject;
            relationB.FriendRelationId.Should().Be(relationId.Value);
            relationB.Friend.Id.Should().Be(userA.Id);
            relationB.Friend.Username.Should().Be(userA.Username);
            relationB.ChatConversationId.Should().Be(relationA.ChatConversationId);
            relationB.DirectConversationId.Should().Be(relationA.DirectConversationId);

            // An accepted request is no longer listed
            (await GetPageAsync<FriendRequestDto>(SentRequestsUrl, userA, ct)).Items.Should().BeEmpty();
            (await GetPageAsync<FriendRequestDto>(ReceivedRequestsUrl, userB, ct)).Items.Should().BeEmpty();
        }

        [Fact]
        public async Task AcceptFriendRequest_BySender_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var friendRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);

            var response = await UpdateFriendRequestAsync(userA, friendRequest.Id, InvitationStatus.Accepted, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            await AssertNotFriendsAsync(userA, userB, ct);
            (await GetPageAsync<FriendRequestDto>(ReceivedRequestsUrl, userB, ct)).Items
                .Should().ContainSingle().Which.Id.Should().Be(friendRequest.Id);
        }

        [Fact]
        public async Task AcceptFriendRequest_ByAnotherUser_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var userC = await CreateUserAsync("user-test-c", ct);
            var friendRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);

            var response = await UpdateFriendRequestAsync(userC, friendRequest.Id, InvitationStatus.Accepted, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            await AssertNotFriendsAsync(userA, userB, ct);
            await AssertNotFriendsAsync(userA, userC, ct);
            (await GetPageAsync<UserFriendResponse>(FriendsUrl, userC, ct)).Items.Should().BeEmpty();
            (await GetPageAsync<FriendRequestDto>(ReceivedRequestsUrl, userB, ct)).Items
                .Should().ContainSingle().Which.Id.Should().Be(friendRequest.Id);
        }

        [Fact]
        public async Task AcceptFriendRequest_Twice_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var friendRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);
            var relationId = await AcceptFriendRequestAsync(userB, friendRequest.Id, ct);

            var response = await UpdateFriendRequestAsync(userB, friendRequest.Id, InvitationStatus.Accepted, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await GetPageAsync<UserFriendResponse>(FriendsUrl, userA, ct)).Items
                .Should().ContainSingle().Which.FriendRelationId.Should().Be(relationId);
        }

        [Fact]
        public async Task AcceptFriendRequest_NotExisting_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);

            var response = await UpdateFriendRequestAsync(userA, Guid.NewGuid(), InvitationStatus.Accepted, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await GetPageAsync<UserFriendResponse>(FriendsUrl, userA, ct)).Items.Should().BeEmpty();
        }

        [Fact]
        public async Task UpdateFriendRequest_PendingStatus_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var friendRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);

            var response = await UpdateFriendRequestAsync(userB, friendRequest.Id, InvitationStatus.Pending, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await GetPageAsync<FriendRequestDto>(ReceivedRequestsUrl, userB, ct)).Items
                .Should().ContainSingle().Which.Status.Should().Be(InvitationStatus.Pending);
        }

        [Fact]
        public async Task UpdateFriendRequest_EmptyId_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);

            var response = await UpdateFriendRequestAsync(userA, Guid.Empty, InvitationStatus.Accepted, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        #endregion

        #region Reject

        [Fact]
        public async Task RejectFriendRequest_ReturnsNoContent_NotFriends()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var friendRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);

            var response = await UpdateFriendRequestAsync(userB, friendRequest.Id, InvitationStatus.Rejected, ct);

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            await AssertNotFriendsAsync(userA, userB, ct);
            (await GetPageAsync<FriendRequestDto>(SentRequestsUrl, userA, ct)).Items.Should().BeEmpty();
            (await GetPageAsync<FriendRequestDto>(ReceivedRequestsUrl, userB, ct)).Items.Should().BeEmpty();
        }

        [Fact]
        public async Task RejectFriendRequest_BySender_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var friendRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);

            var response = await UpdateFriendRequestAsync(userA, friendRequest.Id, InvitationStatus.Rejected, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await GetPageAsync<FriendRequestDto>(ReceivedRequestsUrl, userB, ct)).Items
                .Should().ContainSingle().Which.Status.Should().Be(InvitationStatus.Pending);
        }

        [Fact]
        public async Task RejectFriendRequest_ByAnotherUser_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var userC = await CreateUserAsync("user-test-c", ct);
            var friendRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);

            var response = await UpdateFriendRequestAsync(userC, friendRequest.Id, InvitationStatus.Rejected, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await GetPageAsync<FriendRequestDto>(ReceivedRequestsUrl, userB, ct)).Items
                .Should().ContainSingle().Which.Status.Should().Be(InvitationStatus.Pending);
        }

        [Fact]
        public async Task RejectFriendRequest_AcceptedRequest_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var friendRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);
            await AcceptFriendRequestAsync(userB, friendRequest.Id, ct);

            var response = await UpdateFriendRequestAsync(userB, friendRequest.Id, InvitationStatus.Rejected, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            await AssertFriendsAsync(userA, userB, ct);
        }

        [Fact]
        public async Task AcceptFriendRequest_RejectedRequest_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var friendRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);
            (await UpdateFriendRequestAsync(userB, friendRequest.Id, InvitationStatus.Rejected, ct))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var response = await UpdateFriendRequestAsync(userB, friendRequest.Id, InvitationStatus.Accepted, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            await AssertNotFriendsAsync(userA, userB, ct);
        }

        [Fact]
        public async Task RejectFriendRequest_SentAgain_AndAccepted_ReturnsOk()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var firstRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);
            (await UpdateFriendRequestAsync(userB, firstRequest.Id, InvitationStatus.Rejected, ct))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var secondRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);

            secondRequest.Id.Should().NotBe(firstRequest.Id);
            secondRequest.Status.Should().Be(InvitationStatus.Pending);
            (await GetPageAsync<FriendRequestDto>(ReceivedRequestsUrl, userB, ct)).Items
                .Should().ContainSingle().Which.Id.Should().Be(secondRequest.Id);

            await AcceptFriendRequestAsync(userB, secondRequest.Id, ct);
            await AssertFriendsAsync(userA, userB, ct);
        }

        [Fact]
        public async Task RejectFriendRequest_RejectedUserSendsRequestBack_ReturnsPending()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var firstRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);
            (await UpdateFriendRequestAsync(userB, firstRequest.Id, InvitationStatus.Rejected, ct))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            // The rejected request is not pending, so it is not accepted automatically
            var backRequest = await SendFriendRequestAndReadAsync(userB, userA.Username, ct);

            backRequest.Status.Should().Be(InvitationStatus.Pending);
            backRequest.InvitedById.Should().Be(userB.Id);
            await AssertNotFriendsAsync(userA, userB, ct);

            await AcceptFriendRequestAsync(userA, backRequest.Id, ct);
            await AssertFriendsAsync(userA, userB, ct);
        }

        #endregion

        #region Revoke

        [Fact]
        public async Task RevokeFriendRequest_ReturnsNoContent()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var friendRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);

            var response = await UpdateFriendRequestAsync(userA, friendRequest.Id, InvitationStatus.Revoked, ct);

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            await AssertNotFriendsAsync(userA, userB, ct);
            (await GetPageAsync<FriendRequestDto>(SentRequestsUrl, userA, ct)).Items.Should().BeEmpty();
            (await GetPageAsync<FriendRequestDto>(ReceivedRequestsUrl, userB, ct)).Items.Should().BeEmpty();
        }

        [Fact]
        public async Task RevokeFriendRequest_ByReceiver_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var friendRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);

            var response = await UpdateFriendRequestAsync(userB, friendRequest.Id, InvitationStatus.Revoked, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await GetPageAsync<FriendRequestDto>(SentRequestsUrl, userA, ct)).Items
                .Should().ContainSingle().Which.Status.Should().Be(InvitationStatus.Pending);
        }

        [Fact]
        public async Task RevokeFriendRequest_ByAnotherUser_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var userC = await CreateUserAsync("user-test-c", ct);
            var friendRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);

            var response = await UpdateFriendRequestAsync(userC, friendRequest.Id, InvitationStatus.Revoked, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await GetPageAsync<FriendRequestDto>(SentRequestsUrl, userA, ct)).Items
                .Should().ContainSingle().Which.Status.Should().Be(InvitationStatus.Pending);
        }

        [Fact]
        public async Task RevokeFriendRequest_AcceptedRequest_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var friendRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);
            await AcceptFriendRequestAsync(userB, friendRequest.Id, ct);

            var response = await UpdateFriendRequestAsync(userA, friendRequest.Id, InvitationStatus.Revoked, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            await AssertFriendsAsync(userA, userB, ct);
        }

        [Fact]
        public async Task RevokeFriendRequest_RejectedRequest_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var friendRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);
            (await UpdateFriendRequestAsync(userB, friendRequest.Id, InvitationStatus.Rejected, ct))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var response = await UpdateFriendRequestAsync(userA, friendRequest.Id, InvitationStatus.Revoked, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            await AssertNotFriendsAsync(userA, userB, ct);
        }

        [Fact]
        public async Task AcceptFriendRequest_RevokedRequest_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var friendRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);
            (await UpdateFriendRequestAsync(userA, friendRequest.Id, InvitationStatus.Revoked, ct))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var response = await UpdateFriendRequestAsync(userB, friendRequest.Id, InvitationStatus.Accepted, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            await AssertNotFriendsAsync(userA, userB, ct);
        }

        [Fact]
        public async Task RevokeFriendRequest_SentAgain_AndAccepted_ReturnsOk()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var firstRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);
            (await UpdateFriendRequestAsync(userA, firstRequest.Id, InvitationStatus.Revoked, ct))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var secondRequest = await SendFriendRequestAndReadAsync(userA, userB.Username, ct);

            secondRequest.Id.Should().NotBe(firstRequest.Id);
            secondRequest.Status.Should().Be(InvitationStatus.Pending);

            await AcceptFriendRequestAsync(userB, secondRequest.Id, ct);
            await AssertFriendsAsync(userA, userB, ct);
        }

        #endregion

        #region Friends list

        [Fact]
        public async Task GetFriends_ReturnsOk_Empty()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);

            var friends = await GetPageAsync<UserFriendResponse>(FriendsUrl, userA, ct);

            friends.Items.Should().BeEmpty();
            friends.Total.Should().Be(0);
        }

        [Fact]
        public async Task GetFriends_PendingRequest_IsNotAFriend()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            await SendFriendRequestAndReadAsync(userA, userB.Username, ct);

            await AssertNotFriendsAsync(userA, userB, ct);
        }

        [Fact]
        public async Task GetFriends_OnlyReturnsTheUserFriends()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);
            var userB = await CreateUserAsync("user-test-b", ct);
            var userC = await CreateUserAsync("user-test-c", ct);
            var conversationAB = await MakeFriendsAsync(userA, userB, ct);
            var conversationAC = await MakeFriendsAsync(userA, userC, ct);

            conversationAB.Should().NotBe(conversationAC);

            var friendsA = await GetPageAsync<UserFriendResponse>(FriendsUrl, userA, ct);
            friendsA.Items.Select(x => x.Friend.Id).Should().BeEquivalentTo([userB.Id, userC.Id]);

            var friendsB = await GetPageAsync<UserFriendResponse>(FriendsUrl, userB, ct);
            friendsB.Items.Should().ContainSingle().Which.Friend.Id.Should().Be(userA.Id);

            var friendsC = await GetPageAsync<UserFriendResponse>(FriendsUrl, userC, ct);
            friendsC.Items.Should().ContainSingle().Which.Friend.Id.Should().Be(userA.Id);

            await AssertNotFriendsAsync(userB, userC, ct);
        }

        [Fact]
        public async Task GetFriends_Paginated_SortedByUsername()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("user-test-a", ct);

            int friendsCount = 7;
            var friends = new List<TestUser>();
            for (int i = 0; i < friendsCount; i++)
            {
                var friend = await CreateUserAsync($"user-test-friend-{i:D2}", ct);
                friends.Add(friend);

                // Half of the requests are sent by the user and half are received
                if (i % 2 == 0)
                {
                    await MakeFriendsAsync(userA, friend, ct);
                }
                else
                {
                    await MakeFriendsAsync(friend, userA, ct);
                }
            }

            var sortedUsernames = friends.Select(x => x.Username).OrderBy(x => x, StringComparer.InvariantCultureIgnoreCase).ToList();

            int pageSize = 3;
            Guid? next = null;
            var pagedFriends = new List<UserFriendResponse>();
            do
            {
                var page = await GetPageAsync<UserFriendResponse>(FriendsUrl, userA, ct, new PageRequest
                {
                    Next = next,
                    Descending = false,
                    PageSize = pageSize,
                    SortBy = "username"
                });

                if (page.Next != null)
                {
                    page.Items.Should().HaveCount(pageSize);
                }
                page.Total.Should().Be(friendsCount);

                pagedFriends.AddRange(page.Items);
                next = page.Next;
            } while (next != null);

            pagedFriends.Should().HaveCount(friendsCount);
            pagedFriends.Select(x => x.Friend.Username).Should().ContainInConsecutiveOrder(sortedUsernames);
            pagedFriends.Select(x => x.ChatConversationId).Should().OnlyHaveUniqueItems();
            pagedFriends.Select(x => x.DirectConversationId).Should().OnlyHaveUniqueItems();
            pagedFriends.Select(x => x.FriendRelationId).Should().OnlyHaveUniqueItems();
            pagedFriends.Select(x => x.Friend.Id).Should().BeEquivalentTo(friends.Select(x => x.Id));
        }

        #endregion

        private sealed record TestUser(Guid Id, string Username, string AccessToken);

        private async Task<TestUser> CreateUserAsync(string username, CancellationToken ct)
        {
            var auth = await RegisterAsync(ct, username, $"{username}@test.com");
            auth.AccessToken.Should().NotBeNullOrEmpty();
            return new TestUser(GetUserIdFromAccessToken(auth.AccessToken), username, auth.AccessToken!);
        }

        private Task<HttpResponseMessage> SendFriendRequestAsync(TestUser user, string username, CancellationToken ct)
            => SendAuthorizeHttpRequest(HttpMethod.Post, RequestsUrl, new SendFriendRequest { Username = username }, user.AccessToken, ct);

        private async Task<FriendRequestDto> SendFriendRequestAndReadAsync(TestUser user, string username, CancellationToken ct)
        {
            var response = await SendFriendRequestAsync(user, username, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var friendRequest = await response.Content.ReadFromJsonAsync<FriendRequestDto>(ct);
            friendRequest.Should().NotBeNull();
            return friendRequest!;
        }

        private Task<HttpResponseMessage> UpdateFriendRequestAsync(TestUser user, Guid friendRequestId, InvitationStatus status, CancellationToken ct)
            => SendAuthorizeHttpRequest(HttpMethod.Put, RequestsUrl, new UpdateFriendRequest { Id = friendRequestId, Status = status }, user.AccessToken, ct);

        private async Task<Guid> AcceptFriendRequestAsync(TestUser user, Guid friendRequestId, CancellationToken ct)
        {
            var response = await UpdateFriendRequestAsync(user, friendRequestId, InvitationStatus.Accepted, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var relationId = await response.Content.ReadFromJsonAsync<Guid?>(ct);
            relationId.Should().NotBeNull();
            return relationId!.Value;
        }

        /// <summary>
        /// The first user sends the request and the second one accepts it. Returns their direct conversation.
        /// </summary>
        private async Task<Guid> MakeFriendsAsync(TestUser user, TestUser friend, CancellationToken ct)
        {
            var friendRequest = await SendFriendRequestAndReadAsync(user, friend.Username, ct);
            await AcceptFriendRequestAsync(friend, friendRequest.Id, ct);

            var friends = await GetPageAsync<UserFriendResponse>(FriendsUrl, user, ct, new PageRequest { PageSize = 50 });
            return friends.Items.Single(x => x.Friend.Id == friend.Id).ChatConversationId;
        }

        /// <summary>
        /// PageRequest properties are bound with [FromQuery], so the page is sent in the query string.
        /// </summary>
        private async Task<PageResult<T>> GetPageAsync<T>(string url, TestUser user, CancellationToken ct, PageRequest? page = null)
        {
            var response = await SendAuthorizeHttpRequest(HttpMethod.Get, BuildPageUrl(url, page ?? new PageRequest()), user.AccessToken, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<PageResult<T>>(ct);
            result.Should().NotBeNull();
            return result!;
        }

        private static string BuildPageUrl(string url, PageRequest page)
        {
            var query = new List<string>
            {
                $"pageSize={page.PageSize}",
                $"descending={page.Descending.ToString().ToLowerInvariant()}"
            };
            if (page.Next != null)
                query.Add($"next={page.Next}");
            if (!string.IsNullOrEmpty(page.SortBy))
                query.Add($"sortBy={Uri.EscapeDataString(page.SortBy)}");

            return $"{url}?{string.Join("&", query)}";
        }

        private async Task AssertFriendsAsync(TestUser userA, TestUser userB, CancellationToken ct)
        {
            var friendsA = await GetPageAsync<UserFriendResponse>(FriendsUrl, userA, ct, new PageRequest { PageSize = 50 });
            var friendsB = await GetPageAsync<UserFriendResponse>(FriendsUrl, userB, ct, new PageRequest { PageSize = 50 });

            var relationA = friendsA.Items.Should().ContainSingle(x => x.Friend.Id == userB.Id).Subject;
            var relationB = friendsB.Items.Should().ContainSingle(x => x.Friend.Id == userA.Id).Subject;
            relationA.FriendRelationId.Should().NotBeEmpty().And.Be(relationB.FriendRelationId);
            relationA.ChatConversationId.Should().NotBeEmpty().And.Be(relationB.ChatConversationId);
            relationA.DirectConversationId.Should().NotBeEmpty().And.Be(relationB.DirectConversationId);
        }

        private async Task AssertNotFriendsAsync(TestUser userA, TestUser userB, CancellationToken ct)
        {
            var friendsA = await GetPageAsync<UserFriendResponse>(FriendsUrl, userA, ct, new PageRequest { PageSize = 50 });
            var friendsB = await GetPageAsync<UserFriendResponse>(FriendsUrl, userB, ct, new PageRequest { PageSize = 50 });

            friendsA.Items.Should().NotContain(x => x.Friend.Id == userB.Id);
            friendsB.Items.Should().NotContain(x => x.Friend.Id == userA.Id);
        }

        public void Dispose()
        {
            container.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
