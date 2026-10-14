using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Contracts.Authentication.Responses;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.ChatServers.Requests;
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
                $"/api/v1/chatServers/all",
                auth?.AccessToken!,
                ct
            );

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var servers = await response.Content.ReadFromJsonAsync<IEnumerable<ChatServerDto>>(ct);
            servers.Should().NotBeNull();
            servers.Should().BeEmpty();
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
                HttpMethod.Get, "/api/v1/chatServers/all", auth.AccessToken!, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var serverList = await response.Content.ReadFromJsonAsync<IEnumerable<ChatServerDto>>(ct);
            var serverDto = (serverList)?.FirstOrDefault();
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
                HttpMethod.Get, "/api/v1/chatServers/all", auth.AccessToken!, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var serverList = await response.Content.ReadFromJsonAsync<IEnumerable<ChatServerDto>>(ct);
            serverList.Should().NotBeNull();
            serverList.Should().HaveCount(2);
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
        public void Dispose()
        {
            container.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
