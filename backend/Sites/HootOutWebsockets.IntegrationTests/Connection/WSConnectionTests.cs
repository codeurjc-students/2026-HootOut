using FluentAssertions;
using HootOut.CommonIntegrationTests.Services;
using HootOut.HootOutWebsockets.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.WebSockets;

namespace HootOut.HootOutWebsockets.IntegrationTests.Connection
{
    [Collection("IntegrationTests")]
    public class WSConnectionTests : WSTestBase, IClassFixture<WebSocketServerFixture>, IClassFixture<TestContaineRabbitConnectionFactory>
    {
        public WSConnectionTests(WebSocketServerFixture serverFixture, TestContaineRabbitConnectionFactory rabbitConnectionFactory)
            : base(serverFixture, rabbitConnectionFactory)
        {
        }

        [Fact]
        public async Task Connect_WithoutTicket_Unauthorized()
        {
            var ct = TestContext.Current.CancellationToken;

            await FluentActions.Awaiting(() => WSTestClient.ConnectAsync(serverFixture, wsTicket: "", ct: ct))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("*401*");

            connectionManager.Connections.Should().BeEmpty();
        }

        [Fact]
        public async Task Connect_InvalidTicket_Unauthorized()
        {
            var ct = TestContext.Current.CancellationToken;

            await FluentActions.Awaiting(() => WSTestClient.ConnectAsync(serverFixture, wsTicket: "foo", ct: ct))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("*401*");

            await FluentActions.Awaiting(() => WSTestClient.ConnectAsync(serverFixture, wsTicket: Guid.NewGuid().ToString(), ct: ct))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("*401*");

            connectionManager.Connections.Should().BeEmpty();
        }

        [Fact]
        public async Task Connect_TicketAlreadyUsed_Unauthorized()
        {
            var ct = TestContext.Current.CancellationToken;
            var user = await CreateUserAsync("testuser1");
            var ticket = await authService.CreateWebSocketTicketAsync(user.Id, user.Username, ct);

            await using var socket = await WSTestClient.ConnectAsync(serverFixture, wsTicket: ticket.Ticket, ct: ct);
            await WaitUntilAsync(() => CountConnections(user.Id) == 1);
            socket.State.Should().Be(WebSocketState.Open);

            // The ticket can only be used once
            await FluentActions.Awaiting(() => WSTestClient.ConnectAsync(serverFixture, wsTicket: ticket.Ticket, ct: ct))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("*401*");

            CountConnections(user.Id).Should().Be(1);
            socket.State.Should().Be(WebSocketState.Open);
        }

        [Fact]
        public async Task Connect_NotAWebSocketRequest_BadRequest()
        {
            var ct = TestContext.Current.CancellationToken;
            var client = serverFixture.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

            var response = await client.GetAsync("/ws?ticket=foo", ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            connectionManager.Connections.Should().BeEmpty();
        }

        [Fact]
        public async Task Connect_SeveralUsersAndConnections_AreTrackedSeparately()
        {
            var ct = TestContext.Current.CancellationToken;
            var user1 = await CreateUserAsync("testuser1");
            var user2 = await CreateUserAsync("testuser2");

            await using var socket1 = await ConnectAsync(user1, ct);
            await using var socket2 = await ConnectAsync(user1, ct);
            await using var socket3 = await ConnectAsync(user2, ct);

            connectionManager.Connections.Should().HaveCount(3);
            connectionManager.OutboundQueues.Should().HaveCount(3);
            CountConnections(user1.Id).Should().Be(2);
            CountConnections(user2.Id).Should().Be(1);
            connectionManager.Connections.Values.Should().OnlyContain(x => x.User != null && x.User.Username.Length > 0);

            await socket2.DisposeAsync();
            await WaitUntilAsync(() => connectionManager.Connections.Count == 2);

            CountConnections(user1.Id).Should().Be(1);
            CountConnections(user2.Id).Should().Be(1);
            socket1.State.Should().Be(WebSocketState.Open);
            socket3.State.Should().Be(WebSocketState.Open);
        }

        [Fact]
        public async Task Disconnect_ReleasesConnectionQueueAndSubscriptions()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("testuser1");
            var (serverId, general) = await CreateServerWithChannelAsync(owner, ct);
            var random = await CreateChannelAsync(serverId, ct, "random");

            var socket = await ConnectAsync(owner, ct);
            var connectionId = GetConnectionId(owner.Id);
            connectionManager.OutboundQueues.ContainsKey(connectionId).Should().BeTrue();

            await SubscribeAsync(socket, general, ct);
            await SubscribeAsync(socket, random, ct);
            pubSubService.ConnectionSubscriptions[connectionId].Should().BeEquivalentTo(new[] { general.ToString(), random.ToString() });
            CountSubscribers(general).Should().Be(1);
            CountSubscribers(random).Should().Be(1);

            await socket.DisposeAsync();
            await WaitUntilAsync(() => connectionManager.Connections.IsEmpty && pubSubService.ConnectionSubscriptions.IsEmpty);

            connectionManager.OutboundQueues.ContainsKey(connectionId).Should().BeFalse();
            pubSubService.ChannelSubscribers.ContainsKey(general.ToString()).Should().BeFalse();
            pubSubService.ChannelSubscribers.ContainsKey(random.ToString()).Should().BeFalse();
        }
    }
}
