using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.ChatServers.Requests;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Enums;
using HootOut.HootOutAPI.IntegrationTests.Common;
using System.Net;
using System.Net.Http.Json;

namespace HootOut.HootOutAPI.IntegrationTests.Controllers
{
    /// <summary>
    /// GET /api/v1/chatservers/{id}/members/online and /members/offline.
    /// Nobody has a websocket open in these tests, so every member is offline. The split by presence is tested
    /// in ServerMemberPresenceServiceTests (with Valkey). Without Valkey in the APIFixture the endpoints still answer:
    /// the service treats everybody as offline.
    /// </summary>
    [Collection("IntegrationTests")]
    public class ChatServersControllerMembersPresenceTest : TestControllerBase, IClassFixture<APIFixture>, IAsyncLifetime
    {
        private const string ServersUrl = "/api/v1/chatservers";
        private const string InvitationUrl = "/api/v1/chatservers/invitation";

        private readonly ILifetimeScope container;
        private readonly ClearAllTables clearTables;

        public ChatServersControllerMembersPresenceTest(APIFixture apiFixture)
        {
            httpClient = apiFixture.CreateHttpsClient();
            container = apiFixture.AutofacRoot.BeginLifetimeScope();

            clearTables = container.Resolve<ClearAllTables>();
            foreach (var value in container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority))
            {
                value.InitAsync().Wait();
            }
        }

        public async ValueTask InitializeAsync() => await clearTables.ClearTablesAsync();

        public async ValueTask DisposeAsync()
        {
            await clearTables.ClearTablesAsync();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task GetOnlineMembers_NobodyConnected_ReturnsOkAndEmpty()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("user-test-a", ct);
            var server = await CreateServerAsync(owner, "server-test-a", ct);

            var response = await SendAuthorizeHttpRequest(HttpMethod.Get, $"{ServersUrl}/{server.Id}/members/online", owner.AccessToken, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<OnlineServerMembersDto>(ct);
            result.Should().NotBeNull();
            result!.Items.Should().BeEmpty();
            result.Total.Should().Be(0);
        }

        [Fact]
        public async Task GetOfflineMembers_ReturnsEveryMemberSortedByUsername()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("user-test-b", ct);
            var member = await CreateUserAsync("user-test-a", ct);
            var server = await CreateServerAsync(owner, "server-test-a", ct);
            await JoinAsync(owner, member, server.Id, ct);

            var result = await GetOfflineAsync(owner, $"{ServersUrl}/{server.Id}/members/offline", ct);

            result.Items.Select(m => m.Username).Should().Equal("user-test-a", "user-test-b");
            result.Items.Select(m => m.UserId).Should().Equal(member.Id, owner.Id);
            result.Total.Should().Be(2);
            result.Next.Should().BeNull();
        }

        [Fact]
        public async Task GetOfflineMembers_WithPageSize_ReturnsCursorAndNextPage()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("user-test-a", ct);
            var server = await CreateServerAsync(owner, "server-test-a", ct);
            foreach (var name in new[] { "user-test-b", "user-test-c" })
            {
                await JoinAsync(owner, await CreateUserAsync(name, ct), server.Id, ct);
            }

            var first = await GetOfflineAsync(owner, $"{ServersUrl}/{server.Id}/members/offline?pageSize=2", ct);
            var second = await GetOfflineAsync(owner, $"{ServersUrl}/{server.Id}/members/offline?pageSize=2&next={first.Next}", ct);

            first.Items.Select(m => m.Username).Should().Equal("user-test-a", "user-test-b");
            first.Next.Should().NotBeNull();
            second.Items.Select(m => m.Username).Should().Equal("user-test-c");
            second.Next.Should().BeNull();
        }

        [Theory]
        [InlineData("online")]
        [InlineData("offline")]
        public async Task GetMembersByPresence_NotAMember_ReturnsBadRequest(string list)
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("user-test-a", ct);
            var outsider = await CreateUserAsync("user-test-b", ct);
            var server = await CreateServerAsync(owner, "server-test-a", ct);

            var response = await SendAuthorizeHttpRequest(HttpMethod.Get, $"{ServersUrl}/{server.Id}/members/{list}", outsider.AccessToken, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Theory]
        [InlineData("online")]
        [InlineData("offline")]
        public async Task GetMembersByPresence_ServerDoesNotExist_ReturnsBadRequest(string list)
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("user-test-a", ct);

            var response = await SendAuthorizeHttpRequest(HttpMethod.Get, $"{ServersUrl}/{Guid.NewGuid()}/members/{list}", user.AccessToken, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Theory]
        [InlineData("online")]
        [InlineData("offline")]
        public async Task GetMembersByPresence_WithoutToken_ReturnsUnauthorized(string list)
        {
            var ct = TestContext.Current.CancellationToken;

            var response = await httpClient!.GetAsync($"{ServersUrl}/{Guid.NewGuid()}/members/{list}", ct);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        private sealed record TestUser(Guid Id, string Username, string AccessToken);

        private async Task<TestUser> CreateUserAsync(string username, CancellationToken ct)
        {
            var auth = await RegisterAsync(ct, username, $"{username}@test.com");
            auth.AccessToken.Should().NotBeNullOrEmpty();
            return new TestUser(GetUserIdFromAccessToken(auth.AccessToken), username, auth.AccessToken!);
        }

        private async Task<ChatServerDto> CreateServerAsync(TestUser owner, string name, CancellationToken ct)
        {
            var response = await SendAuthorizeHttpRequest(HttpMethod.Post, ServersUrl, new CreateChatServerRequest { Name = name }, owner.AccessToken, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.Content.ReadFromJsonAsync<ChatServerDto>(ct))!;
        }

        private async Task JoinAsync(TestUser inviter, TestUser user, Guid serverId, CancellationToken ct)
        {
            var invite = await SendAuthorizeHttpRequest(HttpMethod.Post, InvitationUrl,
                new InviteServerMemberRequest { ChatServerId = serverId, Username = user.Username }, inviter.AccessToken, ct);
            invite.StatusCode.Should().Be(HttpStatusCode.OK);
            var invitation = (await invite.Content.ReadFromJsonAsync<ServerInvitationDto>(ct))!;

            var accept = await SendAuthorizeHttpRequest(HttpMethod.Put, InvitationUrl,
                new UpdateServerInvitationRequest { Id = invitation.Id, Status = InvitationStatus.Accepted }, user.AccessToken, ct);
            accept.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        private async Task<PageResult<ServerMemberDto>> GetOfflineAsync(TestUser user, string url, CancellationToken ct)
        {
            var response = await SendAuthorizeHttpRequest(HttpMethod.Get, url, user.AccessToken, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<PageResult<ServerMemberDto>>(ct);
            result.Should().NotBeNull();
            return result!;
        }
    }
}
