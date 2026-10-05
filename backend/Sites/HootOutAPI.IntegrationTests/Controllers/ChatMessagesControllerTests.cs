using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Contracts.Users.Dtos;
using HootOut.HootOutAPI.IntegrationTests.Common;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace HootOut.HootOutAPI.IntegrationTests.Controllers
{
    public class ChatMessagesControllerTests : IClassFixture<APIFixture>, IDisposable, IAsyncLifetime
    {
        private readonly ILifetimeScope container;
        private readonly HttpClient httpClient;
        private ClearAllTables clearTables;

        public ChatMessagesControllerTests(APIFixture apiFixture)
        {
            httpClient = apiFixture.CreateClient();
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
            Guid channelId = Guid.NewGuid();

            var response = await httpClient.GetAsync($"/api/v1/chatMessages/getByChannelId?channelId={channelId.ToString()}", CancellationToken.None);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var messages = await response.Content.ReadFromJsonAsync<IEnumerable<ChatMessageDto>>(CancellationToken.None);
            messages.Should().NotBeNull();
            messages.Should().BeEmpty();
        }

        [Fact]
        public async Task GetMessagesById_ReturnsOk_One()
        {
            Guid channelId = Guid.NewGuid();
            Guid authorId = Guid.NewGuid();
            string content = "test1";

            IChatMessageService chatMessageService = container.Resolve<IChatMessageService>();
            chatMessageService.CreateChatMessage(
                new CreateChatMessageRequest { 
                    ChatChannelId = channelId,
                    AuthorId = authorId,
                    Content = content}
                );

            // store another message so there are more messages
            chatMessageService.CreateChatMessage(
                new CreateChatMessageRequest
                {
                    ChatChannelId = Guid.NewGuid(),
                    AuthorId = authorId,
                    Content = content
                }
                );

            var response = await httpClient.GetAsync($"/api/v1/chatMessages/getByChannelId?channelId={channelId.ToString()}", CancellationToken.None);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var messages = await response.Content.ReadFromJsonAsync<IEnumerable<ChatMessageDto>>(CancellationToken.None);
            messages.Should().NotBeNullOrEmpty();
            messages.Count().Should().Be(1);

            var message = messages.Single();
            message.Id.Should().NotBeEmpty();
            message.Author.Should().NotBeNull();
            message.Author.Id.Should().Be(authorId);
            message.ChatChannelId.Should().Be(channelId);
            message.Content.Should().Be(content);

            // Only receives channel messages
            response = await httpClient.GetAsync($"/api/v1/chatMessages/getByChannelId?channelId={Guid.NewGuid().ToString()}", CancellationToken.None);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            messages = await response.Content.ReadFromJsonAsync<IEnumerable<ChatMessageDto>>(CancellationToken.None);
            messages.Should().NotBeNull();
            messages.Should().BeEmpty();
        }



        public void Dispose()
        {
            container.Dispose();
        }
    }
}
