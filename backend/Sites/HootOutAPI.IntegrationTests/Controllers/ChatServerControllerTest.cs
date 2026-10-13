using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Contracts.Authentication.Responses;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.ChatServers.Requests;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.HootOutAPI.IntegrationTests.Common;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace HootOut.HootOutAPI.IntegrationTests.Controllers
{
    [Collection("IntegrationTests")]
    public class ChatServerControllerTest : TestControllerBase, IClassFixture<APIFixture>, IDisposable, IAsyncLifetime
    {
        private readonly ILifetimeScope container;
        private ClearAllTables clearTables;

        public ChatServerControllerTest(APIFixture apiFixture)
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
                $"/api/v1/chatServer/all",
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
                HttpMethod.Post, "/api/v1/chatServer", new CreateChatServerRequest { Name = name, Description = description }, auth.AccessToken!, ct);
 
            response.StatusCode.Should().Be(HttpStatusCode.OK); 

            var server = await response.Content.ReadFromJsonAsync<ChatServerDto>(ct);
            server.Should().NotBeNull();
            server.Id.Should().NotBeEmpty();
            server.Name.Should().Be(name);
            server.Description.Should().Be(description);
            server.OwnerId.Should().Be(userId);

            response = await SendAuthorizeHttpRequest(
                HttpMethod.Get, "/api/v1/chatServer/all", auth.AccessToken!, ct);
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
                HttpMethod.Post, "/api/v1/chatServer", new CreateChatServerRequest { Name = name, Description = description }, auth.AccessToken!, ct);

            var response = await SendAuthorizeHttpRequest(
                HttpMethod.Post, "/api/v1/chatServer", new CreateChatServerRequest { Name = name, Description = description }, auth.AccessToken!, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var server = await response.Content.ReadFromJsonAsync<ChatServerDto>(ct);
            server.Should().NotBeNull();
            server.Id.Should().NotBeEmpty();
            server.Name.Should().Be(name);
            server.Description.Should().Be(description);
            server.OwnerId.Should().Be(userId);

            response = await SendAuthorizeHttpRequest(
                HttpMethod.Get, "/api/v1/chatServer/all", auth.AccessToken!, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var serverList = await response.Content.ReadFromJsonAsync<IEnumerable<ChatServerDto>>(ct);
            serverList.Should().NotBeNull();
            serverList.Should().HaveCount(2); 
        }


        public void Dispose()
        {
            container.Dispose();
        }
    }
}
