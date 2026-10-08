using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Contracts.Authentication.Responses;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.HootOutAPI.IntegrationTests.Common;
using System.Net;
using System.Net.Http.Json;

namespace HootOut.HootOutAPI.IntegrationTests.Controllers
{
    [Collection("IntegrationTests")]
    public class ChatMessagesControllerTests : TestControllerBase, IClassFixture<APIFixture>, IDisposable, IAsyncLifetime
    {
        private readonly ILifetimeScope container;
        private ClearAllTables clearTables;

        public ChatMessagesControllerTests(APIFixture apiFixture)
        {
            httpClient = apiFixture.CreateHttpsClient();
            container = apiFixture.AutofacRoot.BeginLifetimeScope();

            var testPersistenceProvider = container.Resolve<IPersistenceProvider>();
            Assert.Same(apiFixture.PostgreSQLProvider, testPersistenceProvider);

            clearTables = container.Resolve<ClearAllTables>();
            var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority);

            foreach (var value in defaultValues)
            {
                value.Init();
            }
        }

        public ValueTask InitializeAsync()
        {
            clearTables?.ClearTables();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            clearTables?.ClearTables();
            return ValueTask.CompletedTask;
        }

        [Fact]
        public async Task GetMessagesById_ReturnsOk_Empty()
        {
            var ct = TestContext.Current.CancellationToken;
            await RegisterAsync(ct);

            var loginResponse = await LoginAsync(DefaultEmail, DefaultPassword, ct);
            var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(ct);

            Guid channelId = Guid.NewGuid();
            var response = await SendAuthorizeHttpRequest(
                HttpMethod.Get,
                $"/api/v1/chatMessages/getByChannelId?channelId={channelId.ToString()}",
                auth?.AccessToken!,
                ct
            );

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var messages = await response.Content.ReadFromJsonAsync<IEnumerable<ChatMessageDto>>(ct);
            messages.Should().NotBeNull();
            messages.Should().BeEmpty();
        }

        [Fact]
        public async Task GetMessagesById_ReturnsOk_One()
        {
            var ct = TestContext.Current.CancellationToken;
            await RegisterAsync(ct);

            var loginResponse = await LoginAsync(DefaultEmail, DefaultPassword, ct);
            var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(ct);

            Guid authorId = GetUserIdFromAccessToken(auth?.AccessToken);

            Guid channelId = Guid.NewGuid();
            string content = "test1";

            IChatMessageService chatMessageService = container.Resolve<IChatMessageService>();
            chatMessageService.CreateChatMessage(
                new CreateChatMessageRequest
                {
                    ChatChannelId = channelId,
                    AuthorId = authorId,
                    Content = content
                });

            // store another message so there are more messages
            chatMessageService.CreateChatMessage(
                new CreateChatMessageRequest
                {
                    ChatChannelId = Guid.NewGuid(),
                    AuthorId = authorId,
                    Content = content
                });

            var response = await SendAuthorizeHttpRequest(
                HttpMethod.Get,
                $"/api/v1/chatMessages/getByChannelId?channelId={channelId.ToString()}",
                auth?.AccessToken!,
                ct
            );

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var messages = await response.Content.ReadFromJsonAsync<IEnumerable<ChatMessageDto>>(ct);
            messages.Should().NotBeNullOrEmpty();
            messages.Count().Should().Be(1);

            var message = messages.Single();
            message.Id.Should().NotBeEmpty();
            message.Author.Should().NotBeNull();
            message.Author.Id.Should().Be(authorId);
            message.ChatChannelId.Should().Be(channelId);
            message.Content.Should().Be(content);

            response = await SendAuthorizeHttpRequest(
                HttpMethod.Get,
                $"/api/v1/chatMessages/getByChannelId?channelId={Guid.NewGuid().ToString()}",
                auth?.AccessToken!,
                ct
            );

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            messages = await response.Content.ReadFromJsonAsync<IEnumerable<ChatMessageDto>>(ct);
            messages.Should().NotBeNull();
            messages.Should().BeEmpty();
        }


        public void Dispose()
        {
            container.Dispose();
        }
    }
}
