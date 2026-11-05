using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Contracts.Authentication.Responses;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.ChatServers.Enums;
using HootOut.Contracts.ChatServers.Requests;
using HootOut.Contracts.Common;
using HootOut.HootOutAPI.IntegrationTests.Common;
using System.Net;
using System.Net.Http.Json;

namespace HootOut.HootOutAPI.IntegrationTests.Controllers
{
    [Collection("IntegrationTests")]
    public class ChatServersControllerTest : TestControllerBase, IClassFixture<APIFixture>, IDisposable, IAsyncLifetime
    {
        private readonly ILifetimeScope container;
        private ClearAllTables clearTables;

        public ChatServersControllerTest(APIFixture apiFixture)
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
        public async Task GetServers_ReturnsOk_Empty()
        {
            var ct = TestContext.Current.CancellationToken;
            await RegisterAsync(ct);

            var loginResponse = await LoginAsync(DefaultEmail, DefaultPassword, ct);
            var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(ct);

            Guid channelId = Guid.NewGuid();
            var response = await SendAuthorizeHttpRequest(
                HttpMethod.Get,
                $"/api/v1/chatServers/joined",
                auth?.AccessToken!,
                ct
            );

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var servers = await response.Content.ReadFromJsonAsync<PageResult<ChatServerDto>>(ct);
            servers.Should().NotBeNull();
            servers.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateAndGetServer_ReturnsOk_One()
        {
            var ct = TestContext.Current.CancellationToken;
            await RegisterAsync(ct);

            var loginResponse = await LoginAsync(DefaultEmail, DefaultPassword, ct);
            var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(ct);

            Guid userId = GetUserIdFromAccessToken(auth?.AccessToken);
            string name = "test1";
            string description = "short description of the server";

            var response = await SendAuthorizeHttpRequest(
                HttpMethod.Post, "/api/v1/chatServers", new CreateChatServerRequest { Name = name, Description = description }, auth!.AccessToken!, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var server = await response.Content.ReadFromJsonAsync<ChatServerDto>(ct);
            server.Should().NotBeNull();
            server.Id.Should().NotBeEmpty();
            server.Name.Should().Be(name);
            server.Description.Should().Be(description);
            server.OwnerId.Should().Be(userId);

            response = await SendAuthorizeHttpRequest(
                HttpMethod.Get, "/api/v1/chatServers/joined", auth.AccessToken!, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var serverList = await response.Content.ReadFromJsonAsync<PageResult<ChatServerDto>>(ct);
            var serverDto = (serverList)?.Items.FirstOrDefault();
            serverDto.Should().NotBeNull();
            serverDto.Id.Should().Be(server.Id);
            serverDto.Name.Should().Be(name);
            serverDto.Description.Should().Be(description);
            serverDto.OwnerId.Should().Be(userId);
            serverDto.CreatedAt.Should().BeSameDateAs(server.CreatedAt);
            serverDto.ModifiedAt.Should().BeSameDateAs(server.ModifiedAt);
        }

        [Fact]
        public async Task CreateAndGetServer_ReturnsOk_SameName()
        {
            var ct = TestContext.Current.CancellationToken;
            await RegisterAsync(ct);

            var loginResponse = await LoginAsync(DefaultEmail, DefaultPassword, ct);
            var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(ct);

            Guid userId = GetUserIdFromAccessToken(auth?.AccessToken);
            string name = "test1";
            string description = "short description of the server";

            await SendAuthorizeHttpRequest(
                HttpMethod.Post, "/api/v1/chatServers", new CreateChatServerRequest { Name = name, Description = description }, auth!.AccessToken!, ct);

            var response = await SendAuthorizeHttpRequest(
                HttpMethod.Post, "/api/v1/chatServers", new CreateChatServerRequest { Name = name, Description = description }, auth!.AccessToken!, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var server = await response.Content.ReadFromJsonAsync<ChatServerDto>(ct);
            server.Should().NotBeNull();
            server.Id.Should().NotBeEmpty();
            server.Name.Should().Be(name);
            server.Description.Should().Be(description);
            server.OwnerId.Should().Be(userId);

            response = await SendAuthorizeHttpRequest(
                HttpMethod.Get, "/api/v1/chatServers/joined", auth.AccessToken!, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var serverList = await response.Content.ReadFromJsonAsync<PageResult<ChatServerDto>>(ct);
            serverList.Should().NotBeNull();
            serverList.Items.Should().HaveCount(2);
        }

        [Fact]
        public async Task CreateAndGetServer_ReturnsBadRequest_EmptyName()
        {
            var ct = TestContext.Current.CancellationToken;
            await RegisterAsync(ct);

            var loginResponse = await LoginAsync(DefaultEmail, DefaultPassword, ct);
            var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(ct);

            Guid userId = GetUserIdFromAccessToken(auth?.AccessToken);
            string description = "short description of the server";

            var response = await SendAuthorizeHttpRequest(
                HttpMethod.Post, "/api/v1/chatServers", new CreateChatServerRequest { Name = "", Description = description }, auth!.AccessToken!, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var error = await response.Content.ReadFromJsonAsync<object[]>(ct);
            Console.Write(error?.ToString());
        }

        [Fact]
        public async Task InviteUserToServer_AcceptInvitation_ReturnsOk()
        {
            var ct = TestContext.Current.CancellationToken;
            await RegisterAsync(ct);

            var loginResponse = await LoginAsync(DefaultEmail, DefaultPassword, ct);
            var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(ct);

            Guid userId = GetUserIdFromAccessToken(auth?.AccessToken);
            string description = "short description of the server";

            var response = await SendAuthorizeHttpRequest(
                HttpMethod.Post, "/api/v1/chatServers", new CreateChatServerRequest { Name = "test1234", Description = description }, auth!.AccessToken!, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var server = await response.Content.ReadFromJsonAsync<ChatServerDto>(ct);
            server.Should().NotBeNull();
            server.Id.Should().NotBeEmpty();
            var username2 = "Username 2";

            var auth2 = await RegisterAsync(ct, username2, "email2@test.com");
            Guid userId2 = GetUserIdFromAccessToken(auth2?.AccessToken);

            var invitationResponse = await SendAuthorizeHttpRequest(
                HttpMethod.Post, "/api/v1/chatServers/invitation", new InviteServerMemberRequest { ChatServerId = server.Id, Username = username2 }, auth!.AccessToken!, ct);

            invitationResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var invitation = await invitationResponse.Content.ReadFromJsonAsync<ServerInvitationDto>(ct);
            invitation.Should().NotBeNull();
            invitation.UserId.Should().Be(userId2);
            invitation.ChatServerId.Should().Be(server.Id);
            invitation.Status.Should().Be(ServerInvitationStatus.Pending);

            var invitationAcceptResponse = await SendAuthorizeHttpRequest(
                HttpMethod.Put, "/api/v1/chatServers/invitation", new UpdateServerInvitationRequest { Id = invitation.Id, Status = ServerInvitationStatus.Accepted }, auth2!.AccessToken!, ct);

            invitationAcceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var memberId = await invitationAcceptResponse.Content.ReadFromJsonAsync<Guid?>(ct);
            memberId.Should().NotBeNull().And.NotBeEmpty();

            var serversResponse = await SendAuthorizeHttpRequest(
                HttpMethod.Get, "/api/v1/chatServers/joined", auth2.AccessToken!, ct);
            serversResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var serverList = await serversResponse.Content.ReadFromJsonAsync<PageResult<ChatServerDto>>(ct);
            serverList.Should().NotBeNull();
            serverList.Items.Should().NotBeEmpty().And.HaveCount(1);

            var serverMembers = await SendAuthorizeHttpRequest(
                HttpMethod.Get, $"/api/v1/chatServers/{server.Id}/members?pageSize=20", auth2.AccessToken!, ct);
            serverMembers.StatusCode.Should().Be(HttpStatusCode.OK);

            var membersPage = await serverMembers.Content.ReadFromJsonAsync<PageResult<ServerMemberDto>>(ct);
            membersPage.Should().NotBeNull();
            membersPage.Items.Should().HaveCount(2);
            var member = membersPage.Items.Where(x => x.UserId == userId2);
            member.Should().HaveCount(1);
            member.FirstOrDefault()?.Id.Should().Be(memberId.Value);
        }

        [Fact]
        public async Task InviteUserToServer_RejectsInvitation_ReturnsOk()
        {
            var ct = TestContext.Current.CancellationToken;
            await RegisterAsync(ct);

            var loginResponse = await LoginAsync(DefaultEmail, DefaultPassword, ct);
            var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(ct);

            Guid userId = GetUserIdFromAccessToken(auth?.AccessToken);
            string description = "short description of the server";

            var response = await SendAuthorizeHttpRequest(
                HttpMethod.Post, "/api/v1/chatServers", new CreateChatServerRequest { Name = "test1234", Description = description }, auth!.AccessToken!, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var server = await response.Content.ReadFromJsonAsync<ChatServerDto>(ct);
            server.Should().NotBeNull();
            server.Id.Should().NotBeEmpty();
            var username2 = "Username 2";

            var auth2 = await RegisterAsync(ct, username2, "email2@test.com");
            Guid userId2 = GetUserIdFromAccessToken(auth2?.AccessToken);

            var invitationResponse = await SendAuthorizeHttpRequest(
                HttpMethod.Post, "/api/v1/chatServers/invitation", new InviteServerMemberRequest { ChatServerId = server.Id, Username = username2 }, auth!.AccessToken!, ct);

            invitationResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var invitation = await invitationResponse.Content.ReadFromJsonAsync<ServerInvitationDto>(ct);
            invitation.Should().NotBeNull();
            invitation.UserId.Should().Be(userId2);
            invitation.ChatServerId.Should().Be(server.Id);
            invitation.Status.Should().Be(ServerInvitationStatus.Pending);

            var invitationAcceptResponse = await SendAuthorizeHttpRequest(
                HttpMethod.Put, "/api/v1/chatServers/invitation", new UpdateServerInvitationRequest { Id = invitation.Id, Status = ServerInvitationStatus.Rejected }, auth2!.AccessToken!, ct);

            invitationAcceptResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var serversResponse = await SendAuthorizeHttpRequest(
                HttpMethod.Get, "/api/v1/chatServers/joined", auth2.AccessToken!, ct);
            serversResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var serverList = await serversResponse.Content.ReadFromJsonAsync<PageResult<ChatServerDto>>(ct);
            serverList.Should().NotBeNull();
            serverList.Items.Should().BeEmpty();

            var serverMembers = await SendAuthorizeHttpRequest(
                HttpMethod.Get, $"/api/v1/chatServers/{server.Id}/members", auth.AccessToken!, ct);
            serverMembers.StatusCode.Should().Be(HttpStatusCode.OK);

            var membersPage = await serverMembers.Content.ReadFromJsonAsync<PageResult<ServerMemberDto>>(ct);
            membersPage.Should().NotBeNull();
            membersPage.Items.Should().HaveCount(1);
            var member = membersPage.Items.Where(x => x.UserId == userId2);
            member.Should().BeEmpty();
        }

        [Fact]
        public async Task InviteUserToServer_NotMemberTriesToGetMembers_BadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            await RegisterAsync(ct);

            var loginResponse = await LoginAsync(DefaultEmail, DefaultPassword, ct);
            var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(ct);
            Guid userId = GetUserIdFromAccessToken(auth?.AccessToken);

            var username2 = "Username 2";
            var auth2 = await RegisterAsync(ct, username2, "email2@test.com");
            Guid userId2 = GetUserIdFromAccessToken(auth2?.AccessToken);

            string description = "short description of the server";

            var response = await SendAuthorizeHttpRequest(
                HttpMethod.Post, "/api/v1/chatServers", new CreateChatServerRequest { Name = "test1234", Description = description }, auth!.AccessToken!, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var server = await response.Content.ReadFromJsonAsync<ChatServerDto>(ct);
            server.Should().NotBeNull();
            server.Id.Should().NotBeEmpty();

            var serverMembers = await SendAuthorizeHttpRequest(
                HttpMethod.Get, $"/api/v1/chatServers/{server.Id}/members", auth2!.AccessToken!, ct);
            serverMembers.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        public void Dispose()
        {
            container.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
