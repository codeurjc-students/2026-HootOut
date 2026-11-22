using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
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
    /// POST /api/v1/chatservers/invitation: a user with a pending invitation to the server cannot be invited again.
    /// </summary>
    [Collection("IntegrationTests")]
    public class ChatServersControllerInvitationsTest : TestControllerBase, IClassFixture<APIFixture>, IAsyncLifetime
    {
        private const string ServersUrl = "/api/v1/chatservers";
        private const string InvitationUrl = "/api/v1/chatservers/invitation";

        private readonly ILifetimeScope container;
        private readonly ClearAllTables clearTables;

        public ChatServersControllerInvitationsTest(APIFixture apiFixture)
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

        [Fact]
        public async Task InviteServerMember_ReturnsOk_WithPendingInvitation()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("user-test-a", ct);
            var invitee = await CreateUserAsync("user-test-b", ct);
            var server = await CreateServerAsync(owner, "server-test-a", ct);

            var response = await InviteAsync(owner, server.Id, invitee.Username, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var invitation = await response.Content.ReadFromJsonAsync<ServerInvitationDto>(ct);
            invitation.Should().NotBeNull();
            invitation!.Id.Should().NotBeEmpty();
            invitation.UserId.Should().Be(invitee.Id);
            invitation.ChatServerId.Should().Be(server.Id);
            invitation.Status.Should().Be(InvitationStatus.Pending);
        }

        [Fact]
        public async Task InviteServerMember_SameUserTwice_ReturnsBadRequest_AndKeepsOnePendingInvitation()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("user-test-a", ct);
            var invitee = await CreateUserAsync("user-test-b", ct);
            var server = await CreateServerAsync(owner, "server-test-a", ct);
            var first = await InviteAndReadAsync(owner, server.Id, invitee.Username, ct);

            var response = await InviteAsync(owner, server.Id, invitee.Username, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var invitations = await GetServerInvitationsAsync(owner, server.Id, ct);
            invitations.Items.Should().ContainSingle()
                .Which.Id.Should().Be(first.Id);
            (await GetUserInvitationsAsync(invitee, ct)).Items.Should().ContainSingle()
                .Which.Id.Should().Be(first.Id);
        }

        [Fact]
        public async Task InviteServerMember_OtherMemberInvitesUserWithPendingInvitation_ReturnsBadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("user-test-a", ct);
            var member = await CreateUserAsync("user-test-c", ct);
            var invitee = await CreateUserAsync("user-test-b", ct);
            var server = await CreateServerAsync(owner, "server-test-a", ct);

            // The second member joins the server
            var memberInvitation = await InviteAndReadAsync(owner, server.Id, member.Username, ct);
            (await UpdateInvitationAsync(member, memberInvitation.Id, InvitationStatus.Accepted, ct)).StatusCode.Should().Be(HttpStatusCode.OK);

            var first = await InviteAndReadAsync(owner, server.Id, invitee.Username, ct);

            var response = await InviteAsync(member, server.Id, invitee.Username, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await GetUserInvitationsAsync(invitee, ct)).Items.Should().ContainSingle()
                .Which.Id.Should().Be(first.Id);
        }

        [Theory]
        [InlineData(InvitationStatus.Rejected)]
        [InlineData(InvitationStatus.Revoked)]
        public async Task InviteServerMember_AfterRejectOrRevoke_ReturnsOk(InvitationStatus status)
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("user-test-a", ct);
            var invitee = await CreateUserAsync("user-test-b", ct);
            var server = await CreateServerAsync(owner, "server-test-a", ct);
            var first = await InviteAndReadAsync(owner, server.Id, invitee.Username, ct);

            // The invited user rejects; a member of the server revokes
            var updater = status == InvitationStatus.Rejected ? invitee : owner;
            (await UpdateInvitationAsync(updater, first.Id, status, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

            var response = await InviteAsync(owner, server.Id, invitee.Username, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var second = await response.Content.ReadFromJsonAsync<ServerInvitationDto>(ct);
            second.Should().NotBeNull();
            second!.Id.Should().NotBe(first.Id);
            second.Status.Should().Be(InvitationStatus.Pending);

            var invitations = await GetServerInvitationsAsync(owner, server.Id, ct);
            invitations.Items.Should().HaveCount(2);
            invitations.Items.Single(x => x.Id == first.Id).Status.Should().Be(status);
            invitations.Items.Single(x => x.Id == second.Id).Status.Should().Be(InvitationStatus.Pending);

            // A third invitation is blocked again while the second one is pending
            (await InviteAsync(owner, server.Id, invitee.Username, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task InviteServerMember_SameUserToTwoServers_ReturnsOk()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("user-test-a", ct);
            var invitee = await CreateUserAsync("user-test-b", ct);
            var server1 = await CreateServerAsync(owner, "server-test-a", ct);
            var server2 = await CreateServerAsync(owner, "server-test-b", ct);

            await InviteAndReadAsync(owner, server1.Id, invitee.Username, ct);
            var response = await InviteAsync(owner, server2.Id, invitee.Username, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await GetUserInvitationsAsync(invitee, ct)).Items.Select(x => x.ChatServerId)
                .Should().BeEquivalentTo(new[] { server1.Id, server2.Id });
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

            var server = await response.Content.ReadFromJsonAsync<ChatServerDto>(ct);
            server.Should().NotBeNull();
            return server!;
        }

        private Task<HttpResponseMessage> InviteAsync(TestUser user, Guid serverId, string username, CancellationToken ct)
            => SendAuthorizeHttpRequest(HttpMethod.Post, InvitationUrl, new InviteServerMemberRequest { ChatServerId = serverId, Username = username }, user.AccessToken, ct);

        private async Task<ServerInvitationDto> InviteAndReadAsync(TestUser user, Guid serverId, string username, CancellationToken ct)
        {
            var response = await InviteAsync(user, serverId, username, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var invitation = await response.Content.ReadFromJsonAsync<ServerInvitationDto>(ct);
            invitation.Should().NotBeNull();
            return invitation!;
        }

        private Task<HttpResponseMessage> UpdateInvitationAsync(TestUser user, Guid invitationId, InvitationStatus status, CancellationToken ct)
            => SendAuthorizeHttpRequest(HttpMethod.Put, InvitationUrl, new UpdateServerInvitationRequest { Id = invitationId, Status = status }, user.AccessToken, ct);

        private Task<PageResult<ServerInvitationDto>> GetServerInvitationsAsync(TestUser user, Guid serverId, CancellationToken ct)
            => GetPageAsync<ServerInvitationDto>($"{ServersUrl}/{serverId}/invitations?pageSize=50", user, ct);

        private Task<PageResult<ServerInvitationDto>> GetUserInvitationsAsync(TestUser user, CancellationToken ct)
            => GetPageAsync<ServerInvitationDto>($"{ServersUrl}/invitations?pageSize=50", user, ct);

        private async Task<PageResult<T>> GetPageAsync<T>(string url, TestUser user, CancellationToken ct)
        {
            var response = await SendAuthorizeHttpRequest(HttpMethod.Get, url, user.AccessToken, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<PageResult<T>>(ct);
            result.Should().NotBeNull();
            return result!;
        }
    }
}
