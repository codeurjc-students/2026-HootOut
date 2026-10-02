using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.Persistence;
using HootOut.Contracts.ChatMessage.Dtos;
using HootOut.Contracts.Users.Dtos;
using HootOut.Contracts.WebSocket;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.Contracts.WebSockets.Services;
using HootOut.HootOutWebsockets.IntegrationTests.Common;
using HootOut.HootOutWebsockets.Services;
using HootOut.WebSockets.Service;
using Newtonsoft.Json;
using NLog.Config;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;

namespace HootOut.HootOutWebsockets.IntegrationTests.ServerTests
{
    public  class WSChatMessagesTests: IClassFixture<WebSocketServerFixture>
    {
        private readonly WebSocketServerFixture server;
        private readonly ILifetimeScope container;


        public WSChatMessagesTests(WebSocketServerFixture server)
        {
            this.server = server;
            container = server.AutofacRoot.BeginLifetimeScope();

            var testPersistenceProvider = container.Resolve<IPersistenceProvider>();
            Assert.Same(server.PostgreSQLProvider, testPersistenceProvider);
        }

        [Fact]
        public async Task Websocket_PubSubChatMessage_HappyPath()
        {
            var ct = TestContext.Current.CancellationToken;

            IWSConnectionManager IconnectionManager = container.Resolve<IWSConnectionManager>();

            WSConnectionManager? connectionManager = null;

            if (IconnectionManager is WSConnectionManager)
            {
                connectionManager = IconnectionManager as WSConnectionManager;
            }

            connectionManager!.Connections.Count.Should().Be(0);

            await using var socket1 = await WSTestClient.ConnectAsync(server, ct:ct);
            await Task.Delay(100, ct); //TO-DO Replace with ACK from Server

            socket1.Socket.State.Should().Be(WebSocketState.Open);
            connectionManager!.Connections.Count.Should().Be(1);

            await using var socket2 = await WSTestClient.ConnectAsync(server, ct:ct);
            await Task.Delay(100, ct); //TO-DO Replace with ACK from Server

            socket2.Socket.State.Should().Be(WebSocketState.Open);
            connectionManager!.Connections.Count.Should().Be(2);

            string channel = "testChannel";

            await socket1.SendAsync(new WebSocketMessage
            {
                Type = WSHandlerType.Subscribe,
                Channel = channel
            }, ct);

            await Task.Delay(100, ct); //TO-DO Replace with ACK from Server

            IWSPubSubService IpubSubService = container.Resolve<IWSPubSubService>();
            WSPubSubService? pubSubService = null;

            if (IpubSubService is WSPubSubService)
            {
                pubSubService = (IpubSubService as WSPubSubService)!;
            }

            pubSubService!.ChannelSubscribers.TryGetValue(channel, out var subscribers).Should().BeTrue(); 
            subscribers!.Count.Should().Be(1);

            pubSubService!.ConnectionSubscriptions.Count.Should().Be(1); 

            await socket2.SendAsync(new WebSocketMessage
            {
                Type = WSHandlerType.Subscribe,
                Channel = channel
            }, ct);

            await Task.Delay(100, ct); //TO-DO Replace with ACK from Server

            pubSubService!.ChannelSubscribers.TryGetValue(channel, out subscribers).Should().BeTrue();
            subscribers!.Count.Should().Be(2);
            pubSubService!.ConnectionSubscriptions.Count.Should().Be(2);

            string otherChannel = "Other Channel";
            await socket2.SendAsync(new WebSocketMessage
            {
                Type = WSHandlerType.Subscribe,
                Channel = otherChannel
            }, ct);

            await Task.Delay(100, ct); //TO-DO Replace with ACK from Server

            pubSubService!.ChannelSubscribers.TryGetValue(otherChannel, out subscribers).Should().BeTrue();
            subscribers!.Count.Should().Be(1);
            pubSubService!.ConnectionSubscriptions.Count.Should().Be(2);

            string messageContent = "Test Chat";
            string username = "socket1";
            var chatMessage = new ChatMessageDto
            {
                Content = messageContent,
                Author = new UserDto
                {
                    Username = username, 
                }
            };

            await socket1.SendAsync(new WebSocketMessage
            {
                Type = WSHandlerType.ChatMessage,
                Channel = channel,
                Payload = JsonConvert.SerializeObject(chatMessage)
            }, ct);

            await Task.Delay(100, ct); //TO-DO Replace with ACK from Server

            // Check DB persistance, etc

            var receivedMessage = await socket2.ReceiveAsync(ct);
            receivedMessage.Should().NotBeNull();
            receivedMessage.Channel.Should().Be(channel);

            receivedMessage.Payload.Should().NotBeNullOrWhiteSpace();
            ChatMessageDto? receivedChatMessage = JsonConvert.DeserializeObject<ChatMessageDto>(receivedMessage.Payload);
            receivedChatMessage.Should().NotBeNull();

            receivedChatMessage.Content.Should().Be(messageContent);
            receivedChatMessage.Author.Should().NotBeNull();
            receivedChatMessage.Author.Username.Should().Be(username);

            await socket1.DisposeAsync(); 
            await Task.Delay(100, ct); //TO-DO Replace with ACK from Server

            connectionManager!.Connections.Count.Should().Be(1);

            pubSubService!.ChannelSubscribers.TryGetValue(channel, out subscribers).Should().BeTrue();
            subscribers!.Count.Should().Be(1);
            pubSubService!.ConnectionSubscriptions.Count.Should().Be(1);

            await socket2.DisposeAsync();
            await Task.Delay(100, ct); //TO-DO Replace with ACK from Server

            connectionManager!.Connections.Count.Should().Be(0);

            pubSubService!.ChannelSubscribers.TryGetValue(channel, out subscribers).Should().BeFalse();
            subscribers.Should().BeNull();
            pubSubService!.ConnectionSubscriptions.Should().BeEmpty();
        }
    }
}
