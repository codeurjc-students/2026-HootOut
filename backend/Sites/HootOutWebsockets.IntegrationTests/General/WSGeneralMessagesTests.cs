using FluentAssertions;
using HootOut.CommonIntegrationTests.Services;
using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.HootOutWebsockets.IntegrationTests.Common;
using System.Net.WebSockets;

namespace HootOut.HootOutWebsockets.IntegrationTests.General
{
    [Collection("IntegrationTests")]
    public class WSGeneralMessagesTests : WSTestBase, IClassFixture<WebSocketServerFixture>, IClassFixture<TestContaineRabbitConnectionFactory>
    {
        public WSGeneralMessagesTests(WebSocketServerFixture serverFixture, TestContaineRabbitConnectionFactory rabbitConnectionFactory)
            : base(serverFixture, rabbitConnectionFactory)
        {
        }

        [Fact]
        public async Task InvalidJson_NoReply_ConnectionStaysOpen()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("testuser1");
            var (_, conversationId) = await CreateServerWithChannelAsync(owner, ct);
            await using var socket = await ConnectAsync(owner, ct);

            await socket.SendRawAsync("{not json", ct);
            await socket.SendRawAsync("null", ct);
            await socket.SendRawAsync("""{"id":"foo","type":1}""", ct);
            await socket.SendRawAsync("""{"type":"foo"}""", ct);

            // None of them got an answer: the next message received is the Ack of the subscription
            var subscribe = new WebSocketMessageDto { Id = Guid.NewGuid(), Type = WSHandlerType.Subscribe, Channel = conversationId.ToString() };
            await socket.SendAsync(subscribe, ct);
            var reply = await socket.ReceiveAsync(ct);

            reply.Type.Should().Be(WSHandlerType.Ack);
            reply.ReplyTo.Should().Be(subscribe.Id);
            socket.State.Should().Be(WebSocketState.Open);
            CountSubscribers(conversationId).Should().Be(1);
        }

        [Theory]
        [InlineData(WSHandlerType.Invalid)]
        [InlineData(WSHandlerType.Ack)]
        [InlineData(WSHandlerType.Error)]
        [InlineData((WSHandlerType)99)]
        public async Task UnsupportedType_ReturnsError(WSHandlerType type)
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("testuser1");
            var (_, conversationId) = await CreateServerWithChannelAsync(owner, ct);
            await using var socket = await ConnectAsync(owner, ct);

            var error = await SendExpectingErrorAsync(socket, new WebSocketMessageDto
            {
                Id = Guid.NewGuid(),
                Type = type,
                Channel = conversationId.ToString()
            }, ct);

            error.Should().Be(UnsupportedTypeError);
            pubSubService.ConnectionSubscriptions.Should().BeEmpty();
            (await GetMessagesOldestFirstAsync(conversationId, ct)).Should().BeEmpty();
        }

        [Fact]
        public async Task UnsupportedType_WithoutId_NoReply()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("testuser1");
            await using var socket = await ConnectAsync(owner, ct);

            await socket.SendAsync(new WebSocketMessageDto { Type = (WSHandlerType)99 }, ct);

            (await socket.NoMessageWithinAsync(TimeSpan.FromMilliseconds(300))).Should().BeTrue();
        }

        [Theory]
        [InlineData(WSHandlerType.Subscribe, null)]
        [InlineData(WSHandlerType.Subscribe, "")]
        [InlineData(WSHandlerType.Subscribe, "   ")]
        [InlineData(WSHandlerType.Unsubscribe, null)]
        [InlineData(WSHandlerType.Unsubscribe, "")]
        [InlineData(WSHandlerType.Unsubscribe, "   ")]
        public async Task SubscribeOrUnsubscribe_WithoutChannel_ReturnsError(WSHandlerType type, string? channel)
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("testuser1");
            await using var socket = await ConnectAsync(owner, ct);

            var error = await SendExpectingErrorAsync(socket, new WebSocketMessageDto
            {
                Id = Guid.NewGuid(),
                Type = type,
                Channel = channel
            }, ct);

            error.Should().Be(MissingChannelError);
            pubSubService.ConnectionSubscriptions.Should().BeEmpty();
        }

        [Fact]
        public async Task Subscribe_ChannelIsNotAGuid_ReturnsError()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("testuser1");
            await using var socket = await ConnectAsync(owner, ct);

            var error = await SendExpectingErrorAsync(socket, new WebSocketMessageDto
            {
                Id = Guid.NewGuid(),
                Type = WSHandlerType.Subscribe,
                Channel = "foo"
            }, ct);

            error.Should().Be(InvalidChannelError);
            pubSubService.ChannelSubscribers.ContainsKey("foo").Should().BeFalse();
        }

        [Fact]
        public async Task Unsubscribe_NotSubscribedChannel_DoesNothing()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("testuser1");
            await using var socket = await ConnectAsync(owner, ct);

            var ack = await UnsubscribeAsync(socket, Guid.NewGuid(), ct);

            ack.Type.Should().Be(WSHandlerType.Ack);
            pubSubService.ConnectionSubscriptions.Should().BeEmpty();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("foo")]
        public async Task ChatMessage_InvalidChannel_ReturnsError(string? channel)
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("testuser1");
            var (_, conversationId) = await CreateServerWithChannelAsync(owner, ct);
            await using var socket = await ConnectAsync(owner, ct);

            var message = ChatMessage(conversationId, "hello", Guid.NewGuid());
            message.Channel = channel;

            (await SendExpectingErrorAsync(socket, message, ct)).Should().Be(InvalidChannelError);
            (await GetMessagesOldestFirstAsync(conversationId, ct)).Should().BeEmpty();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("null")]
        [InlineData("{not json")]
        public async Task ChatMessage_InvalidPayload_ReturnsError(string? payload)
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("testuser1");
            var (_, conversationId) = await CreateServerWithChannelAsync(owner, ct);
            await using var socket = await ConnectAsync(owner, ct);

            var message = ChatMessage(conversationId, "hello", Guid.NewGuid());
            message.Payload = payload;

            (await SendExpectingErrorAsync(socket, message, ct)).Should().Be(InvalidPayloadError);
            (await GetMessagesOldestFirstAsync(conversationId, ct)).Should().BeEmpty();
        }

        [Fact]
        public async Task ChatMessage_WithoutId_NoAckButDelivered()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("testuser1");
            var member = await CreateUserAsync("testuser2");
            var (serverId, conversationId) = await CreateServerWithChannelAsync(owner, ct);
            await AddMemberAsync(serverId, owner, member, ct);

            await using var ownerSocket = await ConnectAsync(owner, ct);
            await using var memberSocket = await ConnectAsync(member, ct);
            await SubscribeAsync(ownerSocket, conversationId, ct);
            await SubscribeAsync(memberSocket, conversationId, ct);

            await ownerSocket.SendAsync(ChatMessage(conversationId, "test message 1"), ct);

            await ExpectChatMessageAsync(memberSocket, conversationId, "test message 1", owner, ct);
            (await GetMessagesOldestFirstAsync(conversationId, ct)).Select(x => x.Content).Should().Equal("test message 1");

            (await ownerSocket.NoMessageWithinAsync(TimeSpan.FromMilliseconds(300))).Should().BeTrue();
        }

        [Fact]
        public async Task ChatMessage_BroadcastKeepsTheClientMessageId()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("testuser1");
            var member = await CreateUserAsync("testuser2");
            var (serverId, conversationId) = await CreateServerWithChannelAsync(owner, ct);
            await AddMemberAsync(serverId, owner, member, ct);

            await using var ownerSocket = await ConnectAsync(owner, ct);
            await using var memberSocket = await ConnectAsync(member, ct);
            await SubscribeAsync(memberSocket, conversationId, ct);

            var message = ChatMessage(conversationId, "hello", Guid.NewGuid());
            await ownerSocket.SendWithAckAsync(message, ct);

            var received = await memberSocket.ReceiveAsync(ct);
            received.Id.Should().Be(message.Id);
            received.Type.Should().Be(WSHandlerType.ChatMessage);
            received.ReplyTo.Should().BeNull();
            received.Error.Should().BeNull();
        }

        [Fact]
        public async Task Unsubscribe_StopsReceivingMessages()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("testuser1");
            var member = await CreateUserAsync("testuser2");
            var (serverId, conversationId) = await CreateServerWithChannelAsync(owner, ct);
            await AddMemberAsync(serverId, owner, member, ct);

            await using var ownerSocket = await ConnectAsync(owner, ct);
            await using var memberSocket = await ConnectAsync(member, ct);
            await SubscribeAsync(ownerSocket, conversationId, ct);
            await SubscribeAsync(memberSocket, conversationId, ct);

            await SendChatMessageAsync(ownerSocket, conversationId, "before", ct);
            await ExpectChatMessageAsync(memberSocket, conversationId, "before", owner, ct);

            await UnsubscribeAsync(memberSocket, conversationId, ct);
            CountSubscribers(conversationId).Should().Be(1);

            // Still a member: it can write without being subscribed, it just does not receive
            await SendChatMessageAsync(memberSocket, conversationId, "from unsubscribed member", ct);
            await ExpectChatMessageAsync(ownerSocket, conversationId, "from unsubscribed member", member, ct);

            await SendChatMessageAsync(ownerSocket, conversationId, "after", ct);
            (await GetMessagesOldestFirstAsync(conversationId, ct)).Select(x => x.Content)
                .Should().Equal("before", "from unsubscribed member", "after");

            (await memberSocket.NoMessageWithinAsync(TimeSpan.FromMilliseconds(500))).Should().BeTrue();
        }

        [Fact]
        public async Task ErrorReply_DoesNotCloseTheConnection()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("testuser1");
            var (_, conversationId) = await CreateServerWithChannelAsync(owner, ct);
            await using var socket = await ConnectAsync(owner, ct);

            await SendExpectingErrorAsync(socket, ChatMessage(Guid.NewGuid(), "nowhere", Guid.NewGuid()), ct);
            await SendExpectingErrorAsync(socket, new WebSocketMessageDto { Id = Guid.NewGuid(), Type = (WSHandlerType)99 }, ct);

            socket.State.Should().Be(WebSocketState.Open);
            await SubscribeAsync(socket, conversationId, ct);
            await SendChatMessageAsync(socket, conversationId, "still works", ct);
        }
    }
}
