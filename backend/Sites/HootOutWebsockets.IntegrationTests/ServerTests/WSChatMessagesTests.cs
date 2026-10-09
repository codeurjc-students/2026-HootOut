using Autofac;
using FluentAssertions;
using HootOut.Authentication.Services;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.Services;
using HootOut.Contracts.Authentication.Requests;
using HootOut.Contracts.Authentication.Services;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.ChatMessage.Services;
using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.Contracts.WebSockets.Services;
using HootOut.HootOutWebsockets.Contracts;
using HootOut.HootOutWebsockets.IntegrationTests.Common;
using HootOut.HootOutWebsockets.Services;
using HootOut.RabbitMQ.Configuration;
using HootOut.WebSockets.Service;
using Microsoft.IdentityModel.JsonWebTokens;
using Newtonsoft.Json;
using System.Net.WebSockets;

namespace HootOut.HootOutWebsockets.IntegrationTests.ServerTests
{
    [Collection("IntegrationTests")]
    public class WSChatMessagesTests : IClassFixture<WebSocketServerFixture>, IClassFixture<TestContaineRabbitConnectionFactory>, IDisposable
    {
        private readonly WebSocketServerFixture server;
        private readonly ILifetimeScope container;
        private ClearAllTables clearTables;
        private readonly JsonWebTokenHandler jtwHandler = new();

        private readonly TestContaineRabbitConnectionFactory rabbitConnectionFactory;

        public WSChatMessagesTests(WebSocketServerFixture server, TestContaineRabbitConnectionFactory rabbitConnectionFactory)
        {
            this.server = server;
            var rsaKeyProvider = new DevelopmentRsaKeyProvider("keys/jwt-private.pem");

            this.rabbitConnectionFactory = rabbitConnectionFactory;

            this.server.RegisterDependencies = (ContainerBuilder? builder) =>
            {
                builder?.RegisterInstance(rsaKeyProvider).As<IRsaKeyProvider>().SingleInstance(); //Used for creating the account
                builder?.RegisterInstance(rabbitConnectionFactory).As<IRabbitConnectionFactory>().SingleInstance();
            };

            container = server.AutofacRoot.BeginLifetimeScope();

            var testPersistenceProvider = container.Resolve<IPersistenceProvider>();
            Assert.Same(server.PostgreSQLProvider, testPersistenceProvider);

            var resolvedRabbitConnectionFactory = container.Resolve<IRabbitConnectionFactory>();
            Assert.Same(this.rabbitConnectionFactory, resolvedRabbitConnectionFactory);

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
        public void Dispose()
        {
            container.Dispose();
        }

        [Fact]
        public async Task Websocket_PubSubChatMessage_HappyPath()
        {
            var ct = TestContext.Current.CancellationToken;
            IWSConnectionManager IconnectionManager = container.Resolve<IWSConnectionManager>();
            IAuthenticationService authService = container.Resolve<IAuthenticationService>();

            var username1 = "test1";
            var auth1 = await authService.RegisterUserAsync(new AuthRegistrationRequest
            {
                Username = username1,
                Email = "test@test.com",
                Password = "test123"
            }, ct);
            auth1.AccessToken.Should().NotBeNullOrEmpty();

            var userId1 = Guid.Parse(jtwHandler.ReadJsonWebToken(auth1.AccessToken!).GetClaim("sub").Value);
            var ticket1 = await authService.CreateWebSocketTicketAsync(userId1, username1, ct);

            var username2 = "test2";
            var auth2 = await authService.RegisterUserAsync(new AuthRegistrationRequest
            {
                Username = username2,
                Email = "test2@test.com",
                Password = "test123"
            }, ct);
            auth2.AccessToken.Should().NotBeNullOrEmpty();

            var userId2 = Guid.Parse(jtwHandler.ReadJsonWebToken(auth2.AccessToken!).GetClaim("sub").Value);
            var ticket2 = await authService.CreateWebSocketTicketAsync(userId2, username2, ct);

            WSConnectionManager? connectionManager = null;
            if (IconnectionManager is WSConnectionManager)
            {
                connectionManager = IconnectionManager as WSConnectionManager;
            }

            connectionManager!.Connections.Count.Should().Be(0);

            await using var socket1 = await WSTestClient.ConnectAsync(server, wsTicket: ticket1.Ticket, ct: ct);
            await Task.Delay(100, ct); //TO-DO Replace with ACK from Server

            socket1.Socket.State.Should().Be(WebSocketState.Open);
            connectionManager!.Connections.Count.Should().Be(1);

            await using var socket2 = await WSTestClient.ConnectAsync(server, wsTicket: ticket2.Ticket, ct: ct);
            await Task.Delay(100, ct); //TO-DO Replace with ACK from Server

            socket2.Socket.State.Should().Be(WebSocketState.Open);
            connectionManager!.Connections.Count.Should().Be(2);

            Guid channelId = Guid.NewGuid();

            var id1 = Guid.NewGuid();
            await socket1.SendWithAckAsync(new WebSocketMessageDto
            {
                Type = WSHandlerType.Subscribe,
                Channel = channelId.ToString()
            }, ct);

            IWSPubSubService IpubSubService = container.Resolve<IWSPubSubService>();
            WSPubSubService? pubSubService = null;

            if (IpubSubService is WSPubSubService)
            {
                pubSubService = (IpubSubService as WSPubSubService)!;
            } 
            pubSubService!.ChannelSubscribers.TryGetValue(channelId.ToString(), out var subscribers).Should().BeTrue();
            subscribers!.Count.Should().Be(1);

            pubSubService!.ConnectionSubscriptions.Count.Should().Be(1);

            await socket2.SendWithAckAsync(new WebSocketMessageDto
            {
                Type = WSHandlerType.Subscribe,
                Channel = channelId.ToString()
            }, ct);

            pubSubService!.ChannelSubscribers.TryGetValue(channelId.ToString(), out subscribers).Should().BeTrue();
            subscribers!.Count.Should().Be(2);
            pubSubService!.ConnectionSubscriptions.Count.Should().Be(2);

            Guid otherChannelId = Guid.NewGuid();
            await socket2.SendWithAckAsync(new WebSocketMessageDto
            {
                Type = WSHandlerType.Subscribe,
                Channel = otherChannelId.ToString()
            }, ct);

            pubSubService!.ChannelSubscribers.TryGetValue(otherChannelId.ToString(), out subscribers).Should().BeTrue();
            subscribers!.Count.Should().Be(1);
            pubSubService!.ConnectionSubscriptions.Count.Should().Be(2);

            string messageContent = "Test Chat";
            var chatMessage = new CreateChatMessageRequest
            {
                Content = messageContent,
                AuthorId = userId1
            };

            await socket1.SendWithAckAsync(new WebSocketMessageDto
            {
                Type = WSHandlerType.ChatMessage,
                Channel = channelId.ToString(),
                Payload = JsonConvert.SerializeObject(chatMessage)
            }, ct);

            // Check DB persistance, etc

            IChatMessageService chatMessageService = container.Resolve<IChatMessageService>();

            var messages = chatMessageService.GetMessagesByChannel(channelId);
            messages.Should().NotBeNullOrEmpty();
            messages.Should().HaveCount(1);
            var message1 = messages.Single();
            message1.Should().NotBeNull();
            message1.Author.Should().NotBeNull();
            message1.Author.Id.Should().Be(userId1);
            message1.ChatChannelId.Should().Be(channelId);
            message1.Content.Should().Be(messageContent);

            var receivedMessage = await socket2.ReceiveAsync(ct);
            receivedMessage.Should().NotBeNull();
            receivedMessage.Channel.Should().Be(channelId.ToString());

            receivedMessage.Payload.Should().NotBeNullOrWhiteSpace();
            ChatMessageDto? receivedChatMessage = JsonConvert.DeserializeObject<ChatMessageDto>(receivedMessage.Payload);
            receivedChatMessage.Should().NotBeNull();

            receivedChatMessage.Content.Should().Be(messageContent);
            receivedChatMessage.Author.Should().NotBeNull();
            receivedChatMessage.Author.Id.Should().Be(userId1);

            await socket1.DisposeAsync();
            await Task.Delay(100, ct);

            connectionManager!.Connections.Count.Should().Be(1);

            pubSubService!.ChannelSubscribers.TryGetValue(channelId.ToString(), out subscribers).Should().BeTrue();
            subscribers!.Count.Should().Be(1);
            pubSubService!.ConnectionSubscriptions.Count.Should().Be(1);

            await socket2.DisposeAsync();
            await Task.Delay(100, ct);

            connectionManager!.Connections.Count.Should().Be(0);

            pubSubService!.ChannelSubscribers.TryGetValue(channelId.ToString(), out subscribers).Should().BeFalse();
            subscribers.Should().BeNull();
            pubSubService!.ConnectionSubscriptions.Should().BeEmpty();
        }
    }
}
