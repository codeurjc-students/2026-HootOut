using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Contracts.Authentication.Responses;
using HootOut.Contracts.ChatChannels.Dtos;
using HootOut.Contracts.ChatChannels.Requests;
using HootOut.Contracts.ChatServers.Dtos;
using HootOut.Contracts.ChatServers.Requests;
using HootOut.HootOutAPI.IntegrationTests.Common;
using System.Net;
using System.Net.Http.Json;

namespace HootOut.HootOutAPI.IntegrationTests.Controllers
{
    [Collection("IntegrationTests")]
    public class ChatChannelsControllerTests : TestControllerBase, IClassFixture<APIFixture>, IDisposable, IAsyncLifetime
    {
        private readonly ILifetimeScope container;
        private ClearAllTables clearTables;

        public ChatChannelsControllerTests(APIFixture apiFixture)
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
                $"/api/v1/chatChannels/getByServerId?serverId={Guid.NewGuid()}",
                auth?.AccessToken!,
                ct
            );

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var servers = await response.Content.ReadFromJsonAsync<IEnumerable<ChatChannelDto>>(ct);
            servers.Should().NotBeNull();
            servers.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateAndGetChatChannelsByServerId_ReturnsOk_One()
        {
            var ct = TestContext.Current.CancellationToken;
            await RegisterAsync(ct);

            var loginResponse = await LoginAsync(DefaultEmail, DefaultPassword, ct);
            var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(ct);

            Guid userId = GetUserIdFromAccessToken(auth?.AccessToken);
            string serverName = "test1";
            string description = "short description of the server";

            var response = await SendAuthorizeHttpRequest(
                HttpMethod.Post, "/api/v1/chatServers", new CreateChatServerRequest { Name = serverName, Description = description }, auth!.AccessToken!, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var server = await response.Content.ReadFromJsonAsync<ChatServerDto>(ct);
            server.Should().NotBeNull();
            server.Id.Should().NotBeEmpty();
            server.Name.Should().Be(serverName);
            server.Description.Should().Be(description);
            server.OwnerId.Should().Be(userId);

            string channelName = "Channel Test";
            response = await SendAuthorizeHttpRequest(
               HttpMethod.Post, "/api/v1/chatChannels", new CreateChatChannelRequest { Name = channelName, ChatServerId = server.Id }, auth!.AccessToken!, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var channel = await response.Content.ReadFromJsonAsync<ChatChannelDto>(ct);
            channel.Should().NotBeNull();
            channel.Id.Should().NotBeEmpty();
            channel.Name.Should().Be(channelName);
            channel.ChatServerId.Should().Be(server.Id);

            response = await SendAuthorizeHttpRequest(
                HttpMethod.Get, $"/api/v1/chatChannels/getByServerId?serverId={server.Id.ToString()}", auth.AccessToken!, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var channelList = await response.Content.ReadFromJsonAsync<IEnumerable<ChatChannelDto>>(ct);
            channelList.Should().NotBeNullOrEmpty();
            var channelDto = (channelList)?.FirstOrDefault();
            channelDto.Should().NotBeNull();
            channelDto.Id.Should().Be(channel.Id);
            channelDto.Name.Should().Be(channel.Name);
            channelDto.ChatServerId.Should().Be(channel.ChatServerId);
            channelDto.CreatedAt.Should().BeSameDateAs(channel.CreatedAt);
            channelDto.ModifiedAt.Should().BeSameDateAs(channel.ModifiedAt);
        }

        [Fact]
        public async Task CreateAndUpdateChannel_ReturnsOk_One()
        {
            var ct = TestContext.Current.CancellationToken;
            await RegisterAsync(ct);

            var loginResponse = await LoginAsync(DefaultEmail, DefaultPassword, ct);
            var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(ct);

            Guid userId = GetUserIdFromAccessToken(auth?.AccessToken);
            string serverName = "test1";
            string description = "short description of the server";

            var response = await SendAuthorizeHttpRequest(
                HttpMethod.Post, "/api/v1/chatServers", new CreateChatServerRequest { Name = serverName, Description = description }, auth!.AccessToken!, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var server = await response.Content.ReadFromJsonAsync<ChatServerDto>(ct);
            server.Should().NotBeNull();
            server.Id.Should().NotBeEmpty();

            string channelName = "Channel Test";
            response = await SendAuthorizeHttpRequest(
               HttpMethod.Post, "/api/v1/chatChannels", new CreateChatChannelRequest { Name = channelName, ChatServerId = server.Id }, auth!.AccessToken!, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var channel = await response.Content.ReadFromJsonAsync<ChatChannelDto>(ct);
            channel.Should().NotBeNull();
            channel.Id.Should().NotBeEmpty();
            channel.Name.Should().Be(channelName);
            channel.ChatServerId.Should().Be(server.Id);

            string newName = "New Name";
            response = await SendAuthorizeHttpRequest(
               HttpMethod.Put, "/api/v1/chatChannels", new UpdateChatChannelRequest { Name = newName, Id = channel.Id }, auth!.AccessToken!, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var updatedChannel = await response.Content.ReadFromJsonAsync<ChatChannelDto>(ct);
            updatedChannel.Should().NotBeNull();
            updatedChannel.Id.Should().Be(channel.Id);
            updatedChannel.Name.Should().Be(newName);
            updatedChannel.ChatServerId.Should().Be(channel.ChatServerId);

            response = await SendAuthorizeHttpRequest(
                HttpMethod.Get, $"/api/v1/chatChannels/getByServerId?serverId={server.Id.ToString()}", auth.AccessToken!, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var channelList = await response.Content.ReadFromJsonAsync<IEnumerable<ChatChannelDto>>(ct);
            channelList.Should().NotBeNullOrEmpty();
            var channelDto = (channelList)?.FirstOrDefault();
            channelDto.Should().NotBeNull();
            channelDto.Id.Should().Be(channel.Id);
            channelDto.Name.Should().Be(newName);
            channelDto.ChatServerId.Should().Be(channel.ChatServerId);
            channelDto.CreatedAt.Should().BeSameDateAs(channel.CreatedAt);
            channelDto.ModifiedAt.Should().BeSameDateAs(updatedChannel.ModifiedAt);
        }

        [Fact]
        public async Task CreateAndDeleteChannel_ReturnsOk_One()
        {
            var ct = TestContext.Current.CancellationToken;
            await RegisterAsync(ct);

            var loginResponse = await LoginAsync(DefaultEmail, DefaultPassword, ct);
            var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(ct);

            Guid userId = GetUserIdFromAccessToken(auth?.AccessToken);
            string serverName = "test1";
            string description = "short description of the server";

            var response = await SendAuthorizeHttpRequest(
                HttpMethod.Post, "/api/v1/chatServers", new CreateChatServerRequest { Name = serverName, Description = description }, auth!.AccessToken!, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var server = await response.Content.ReadFromJsonAsync<ChatServerDto>(ct);
            server.Should().NotBeNull();
            server.Id.Should().NotBeEmpty();

            string channelName = "Channel Test";
            response = await SendAuthorizeHttpRequest(
               HttpMethod.Post, "/api/v1/chatChannels", new CreateChatChannelRequest { Name = channelName, ChatServerId = server.Id }, auth!.AccessToken!, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var channel = await response.Content.ReadFromJsonAsync<ChatChannelDto>(ct);
            channel.Should().NotBeNull();
            channel.Id.Should().NotBeEmpty();
            channel.Name.Should().Be(channelName);
            channel.ChatServerId.Should().Be(server.Id);

            response = await SendAuthorizeHttpRequest(
               HttpMethod.Delete, $"/api/v1/chatChannels/{channel.Id.ToString()}", auth!.AccessToken!, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var id = await response.Content.ReadFromJsonAsync<Guid>(ct);
            id.Should().NotBeEmpty();
            id.Should().Be(channel.Id);

            response = await SendAuthorizeHttpRequest(
                HttpMethod.Get, $"/api/v1/chatChannels/getByServerId?serverId={server.Id.ToString()}", auth.AccessToken!, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var channelList = await response.Content.ReadFromJsonAsync<IEnumerable<ChatChannelDto>>(ct);
            channelList.Should().BeEmpty();
        }


        public void Dispose()
        {
            container.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
