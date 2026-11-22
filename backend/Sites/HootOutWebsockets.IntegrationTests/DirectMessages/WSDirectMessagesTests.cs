using FluentAssertions;
using HootOut.CommonIntegrationTests.Services;
using HootOut.Contracts.ChatMessage.Requests;
using HootOut.Contracts.Common;
using HootOut.Contracts.Common.Enums;
using HootOut.Contracts.Friends.Requests;
using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.HootOutWebsockets.IntegrationTests.Common;

namespace HootOut.HootOutWebsockets.IntegrationTests.DirectMessages
{
    [Collection("IntegrationTests")]
    public class WSDirectMessagesTests : WSTestBase, IClassFixture<WebSocketServerFixture>, IClassFixture<TestContaineRabbitConnectionFactory>
    {
        public WSDirectMessagesTests(WebSocketServerFixture serverFixture, TestContaineRabbitConnectionFactory rabbitConnectionFactory)
            : base(serverFixture, rabbitConnectionFactory)
        {
        }

        [Fact]
        public async Task DirectMessages_Friends_SendAndReceiveBothWays()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var (_, conversationId) = await MakeFriendsAsync(userA, userB, ct);

            await using var socketA = await ConnectAsync(userA, ct);
            await using var socketB = await ConnectAsync(userB, ct);

            await SubscribeAsync(socketA, conversationId, ct);
            await SubscribeAsync(socketB, conversationId, ct);
            CountSubscribers(conversationId).Should().Be(2);

            // A -> B
            var savedA = await SendChatMessageAsync(socketA, conversationId, "hello", ct);
            savedA.Id.Should().NotBeEmpty();
            savedA.Author.Should().NotBeNull();
            savedA.Author!.Id.Should().Be(userA.Id);
            savedA.Author.Username.Should().Be(userA.Username);

            var receivedB = await ExpectChatMessageAsync(socketB, conversationId, "hello", userA, ct);
            receivedB.Id.Should().Be(savedA.Id);

            // B -> A. A must not receive its own message first
            var savedB = await SendChatMessageAsync(socketB, conversationId, "hi there", ct);
            var receivedA = await ExpectChatMessageAsync(socketA, conversationId, "hi there", userB, ct);
            receivedA.Id.Should().Be(savedB.Id);

            var messages = await GetMessagesOldestFirstAsync(conversationId, ct);
            messages.Select(x => x.Id).Should().Equal(savedA.Id, savedB.Id);
            messages.Select(x => x.Content).Should().Equal("hello", "hi there");
            messages.Select(x => x.Author!.Id).Should().Equal(userA.Id, userB.Id);

            (await socketA.NoMessageWithinAsync(TimeSpan.FromMilliseconds(300))).Should().BeTrue();
            (await socketB.NoMessageWithinAsync(TimeSpan.FromMilliseconds(300))).Should().BeTrue();
        }

        [Fact]
        public async Task DirectMessages_UserOutsideTheConversation_CannotSubscribeOrSend()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var userC = await CreateUserAsync("userC1");
            var (_, conversationAB) = await MakeFriendsAsync(userA, userB, ct);
            // C is friends with both of them, but the conversation between A and B is not theirs
            await MakeFriendsAsync(userC, userA, ct);
            await MakeFriendsAsync(userC, userB, ct);

            await using var socketA = await ConnectAsync(userA, ct);
            await using var socketB = await ConnectAsync(userB, ct);
            await using var socketC = await ConnectAsync(userC, ct);
            await SubscribeAsync(socketA, conversationAB, ct);
            await SubscribeAsync(socketB, conversationAB, ct);

            // C cannot subscribe
            var subscribeError = await SendExpectingErrorAsync(socketC, new WebSocketMessageDto
            {
                Id = Guid.NewGuid(),
                Type = WSHandlerType.Subscribe,
                Channel = conversationAB.ToString()
            }, ct);
            subscribeError.Should().Be(CannotSubscribeError);
            CountSubscribers(conversationAB).Should().Be(2);
            pubSubService.ConnectionSubscriptions.ContainsKey(GetConnectionId(userC.Id)).Should().BeFalse();

            // C cannot send
            var sendError = await SendExpectingErrorAsync(socketC, ChatMessage(conversationAB, "intruder", Guid.NewGuid()), ct);
            sendError.Should().Be(CannotSendMessageError);

            // A and B did not receive the intruder message: the next one they get is A's
            await SendChatMessageAsync(socketA, conversationAB, "only friends", ct);
            await ExpectChatMessageAsync(socketB, conversationAB, "only friends", userA, ct);

            (await GetMessagesOldestFirstAsync(conversationAB, ct)).Select(x => x.Content).Should().Equal("only friends");

            (await socketC.NoMessageWithinAsync(TimeSpan.FromMilliseconds(300))).Should().BeTrue();
        }

        [Fact]
        public async Task DirectMessages_NotExistingConversation_CannotSubscribeOrSend()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var conversationId = Guid.NewGuid();

            await using var socketA = await ConnectAsync(userA, ct);

            (await SendExpectingErrorAsync(socketA, new WebSocketMessageDto
            {
                Id = Guid.NewGuid(),
                Type = WSHandlerType.Subscribe,
                Channel = conversationId.ToString()
            }, ct)).Should().Be(CannotSubscribeError);

            (await SendExpectingErrorAsync(socketA, ChatMessage(conversationId, "hello", Guid.NewGuid()), ct))
                .Should().Be(CannotSendMessageError);

            CountSubscribers(conversationId).Should().Be(0);
            pubSubService.ChannelSubscribers.ContainsKey(conversationId.ToString()).Should().BeFalse();
        }

        [Fact]
        public async Task DirectMessages_ExFriends_CannotSubscribeOrSend()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var (relationId, conversationId) = await MakeFriendsAsync(userA, userB, ct);

            await using var socketA = await ConnectAsync(userA, ct);
            await SubscribeAsync(socketA, conversationId, ct);
            await SendChatMessageAsync(socketA, conversationId, "while friends", ct);

            await DeleteFriendRelationAsync(relationId, ct);

            // A cannot write anymore
            (await SendExpectingErrorAsync(socketA, ChatMessage(conversationId, "after", Guid.NewGuid()), ct))
                .Should().Be(CannotSendMessageError);

            // B cannot subscribe anymore
            await using var socketB = await ConnectAsync(userB, ct);
            (await SendExpectingErrorAsync(socketB, new WebSocketMessageDto
            {
                Id = Guid.NewGuid(),
                Type = WSHandlerType.Subscribe,
                Channel = conversationId.ToString()
            }, ct)).Should().Be(CannotSubscribeError);

            // B cannot write either
            (await SendExpectingErrorAsync(socketB, ChatMessage(conversationId, "after", Guid.NewGuid()), ct))
                .Should().Be(CannotSendMessageError);

            // The history is kept, but nothing new was saved
            (await GetMessagesOldestFirstAsync(conversationId, ct)).Select(x => x.Content).Should().Equal("while friends");
        }

        [Fact]
        public async Task DirectMessages_PendingFriendRequest_CannotSendMessages()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var userC = await CreateUserAsync("userC1");

            // A pending request between A and C does not create a conversation
            await SendFriendRequestAsync(userA, userC, ct);
            (await friendsService.GetUserFriendsAsync(new PageRequest(), userA.Id, ct)).Items.Should().BeEmpty();
            (await friendsService.GetUserFriendsAsync(new PageRequest(), userC.Id, ct)).Items.Should().BeEmpty();

            // A and B were friends; A sends a new request that is still pending
            var (relationId, conversationAB) = await MakeFriendsAsync(userA, userB, ct);
            await DeleteFriendRelationAsync(relationId, ct);
            var pendingRequestId = await SendFriendRequestAsync(userA, userB, ct);

            await using var socketA = await ConnectAsync(userA, ct);
            await using var socketB = await ConnectAsync(userB, ct);

            // With the request pending neither of them can use their old conversation
            (await SendExpectingErrorAsync(socketA, ChatMessage(conversationAB, "pending", Guid.NewGuid()), ct))
                .Should().Be(CannotSendMessageError);
            (await SendExpectingErrorAsync(socketB, ChatMessage(conversationAB, "pending", Guid.NewGuid()), ct))
                .Should().Be(CannotSendMessageError);
            (await SendExpectingErrorAsync(socketB, new WebSocketMessageDto
            {
                Id = Guid.NewGuid(),
                Type = WSHandlerType.Subscribe,
                Channel = conversationAB.ToString()
            }, ct)).Should().Be(CannotSubscribeError);

            (await GetMessagesOldestFirstAsync(conversationAB, ct)).Should().BeEmpty();

            // Once B accepts, they can talk again in the same conversation
            await friendsService.UpdateFriendRequestAsync(new UpdateFriendRequest
            {
                Id = pendingRequestId,
                Status = InvitationStatus.Accepted
            }, userB.Id, ct);

            await SubscribeAsync(socketB, conversationAB, ct);
            await SendChatMessageAsync(socketA, conversationAB, "accepted", ct);
            await ExpectChatMessageAsync(socketB, conversationAB, "accepted", userA, ct);
        }

        [Fact]
        public async Task DirectMessages_FriendsAgain_ReuseConversationAndHistory()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var (relationId, conversationId) = await MakeFriendsAsync(userA, userB, ct);

            await using var socketA = await ConnectAsync(userA, ct);
            await using var socketB = await ConnectAsync(userB, ct);
            await SubscribeAsync(socketA, conversationId, ct);
            await SubscribeAsync(socketB, conversationId, ct);

            await SendChatMessageAsync(socketA, conversationId, "first time", ct);
            await ExpectChatMessageAsync(socketB, conversationId, "first time", userA, ct);

            await DeleteFriendRelationAsync(relationId, ct);

            // This time B sends the request
            var (newRelationId, newConversationId) = await MakeFriendsAsync(userB, userA, ct);
            newRelationId.Should().NotBe(relationId);
            newConversationId.Should().Be(conversationId);

            await SendChatMessageAsync(socketB, conversationId, "second time", ct);
            await ExpectChatMessageAsync(socketA, conversationId, "second time", userB, ct);

            var messages = await GetMessagesOldestFirstAsync(conversationId, ct);
            messages.Select(x => x.Content).Should().Equal("first time", "second time");
            messages.Select(x => x.Author!.Id).Should().Equal(userA.Id, userB.Id);
        }

        [Fact]
        public async Task DirectMessages_PayloadAuthorAndConversation_AreIgnored()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var userC = await CreateUserAsync("userC1");
            var (_, conversationAB) = await MakeFriendsAsync(userA, userB, ct);
            var (_, conversationAC) = await MakeFriendsAsync(userA, userC, ct);

            await using var socketA = await ConnectAsync(userA, ct);
            await using var socketB = await ConnectAsync(userB, ct);
            await SubscribeAsync(socketB, conversationAB, ct);

            // A tries to write as B and in the conversation with C, but the socket channel is AB
            var message = ChatMessage(conversationAB, new CreateChatMessageRequest
            {
                Content = "spoofed",
                AuthorId = userB.Id,
                ChatConversationId = conversationAC
            }, Guid.NewGuid());

            var ack = await socketA.SendWithAckAsync(message, ct);
            var saved = ReadChatMessage(ack);
            saved.Author!.Id.Should().Be(userA.Id);
            saved.ChatConversationId.Should().Be(conversationAB);

            await ExpectChatMessageAsync(socketB, conversationAB, "spoofed", userA, ct);

            var messagesAB = await GetMessagesOldestFirstAsync(conversationAB, ct);
            messagesAB.Should().ContainSingle();
            messagesAB[0].Author!.Id.Should().Be(userA.Id);
            (await GetMessagesOldestFirstAsync(conversationAC, ct)).Should().BeEmpty();
        }

        [Fact]
        public async Task DirectMessages_ManyMessagesInARow_AreDeliveredInOrder()
        {
            var ct = TestContext.Current.CancellationToken;
            const int count = 20;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var (_, conversationId) = await MakeFriendsAsync(userA, userB, ct);

            await using var socketA = await ConnectAsync(userA, ct);
            await using var socketB = await ConnectAsync(userB, ct);
            await SubscribeAsync(socketA, conversationId, ct);
            await SubscribeAsync(socketB, conversationId, ct);

            // A sends all the messages without waiting for the Acks
            var sent = Enumerable.Range(1, count).Select(i => ChatMessage(conversationId, $"message {i}", Guid.NewGuid())).ToList();
            foreach (var message in sent)
            {
                await socketA.SendAsync(message, ct);
            }

            // A gets one Ack per message, in the same order
            var acks = await socketA.ReceiveManyAsync(count, ct);
            acks.Should().OnlyContain(x => x.Type == WSHandlerType.Ack && x.Error == null);
            acks.Select(x => x.ReplyTo).Should().Equal(sent.Select(x => x.Id));
            acks.Select(x => ReadChatMessage(x).Content).Should().Equal(sent.Select((_, i) => $"message {i + 1}"));

            // B gets every message once and in order
            var received = await socketB.ReceiveManyAsync(count, ct);
            received.Should().OnlyContain(x => x.Type == WSHandlerType.ChatMessage);
            received.Select(x => ReadChatMessage(x).Content).Should().Equal(sent.Select((_, i) => $"message {i + 1}"));

            var messages = await GetMessagesOldestFirstAsync(conversationId, ct);
            messages.Select(x => x.Content).Should().Equal(sent.Select((_, i) => $"message {i + 1}"));
            messages.Select(x => x.Id).Should().Equal(acks.Select(x => ReadChatMessage(x).Id));

            (await socketB.NoMessageWithinAsync(TimeSpan.FromMilliseconds(300))).Should().BeTrue();
        }

        [Fact]
        public async Task DirectMessages_BothFriendsWritingAtTheSameTime_KeepEachSenderOrder()
        {
            var ct = TestContext.Current.CancellationToken;
            const int count = 10;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var (_, conversationId) = await MakeFriendsAsync(userA, userB, ct);

            await using var socketA = await ConnectAsync(userA, ct);
            await using var socketB = await ConnectAsync(userB, ct);
            await SubscribeAsync(socketA, conversationId, ct);
            await SubscribeAsync(socketB, conversationId, ct);

            var contentsA = Enumerable.Range(1, count).Select(i => $"from A {i}").ToList();
            var contentsB = Enumerable.Range(1, count).Select(i => $"from B {i}").ToList();

            async Task SendAllAsync(WSTestClient socket, List<string> contents)
            {
                foreach (var content in contents)
                {
                    await socket.SendAsync(ChatMessage(conversationId, content, Guid.NewGuid()), ct);
                }
            }

            await Task.WhenAll(SendAllAsync(socketA, contentsA), SendAllAsync(socketB, contentsB));

            // Each socket gets its own Acks and the other's messages, interleaved in any way
            var receivedA = await socketA.ReceiveManyAsync(count * 2, ct);
            var receivedB = await socketB.ReceiveManyAsync(count * 2, ct);

            receivedA.Where(x => x.Type == WSHandlerType.Ack).Select(x => ReadChatMessage(x).Content).Should().Equal(contentsA);
            receivedA.Where(x => x.Type == WSHandlerType.ChatMessage).Select(x => ReadChatMessage(x).Content).Should().Equal(contentsB);
            receivedB.Where(x => x.Type == WSHandlerType.Ack).Select(x => ReadChatMessage(x).Content).Should().Equal(contentsB);
            receivedB.Where(x => x.Type == WSHandlerType.ChatMessage).Select(x => ReadChatMessage(x).Content).Should().Equal(contentsA);

            var messages = await GetMessagesOldestFirstAsync(conversationId, ct);
            messages.Should().HaveCount(count * 2);
            messages.Where(x => x.Author!.Id == userA.Id).Select(x => x.Content).Should().Equal(contentsA);
            messages.Where(x => x.Author!.Id == userB.Id).Select(x => x.Content).Should().Equal(contentsB);
        }

        [Fact]
        public async Task DirectMessages_SubscribeSeveralTimes_ReceivesEachMessageOnce()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var (_, conversationId) = await MakeFriendsAsync(userA, userB, ct);

            await using var socketA = await ConnectAsync(userA, ct);
            await using var socketB = await ConnectAsync(userB, ct);

            for (var i = 0; i < 3; i++)
            {
                await SubscribeAsync(socketB, conversationId, ct);
            }

            CountSubscribers(conversationId).Should().Be(1);
            pubSubService.ConnectionSubscriptions[GetConnectionId(userB.Id)].Should().BeEquivalentTo(new[] { conversationId.ToString() });

            await SendChatMessageAsync(socketA, conversationId, "one", ct);
            await SendChatMessageAsync(socketA, conversationId, "two", ct);

            await ExpectChatMessageAsync(socketB, conversationId, "one", userA, ct);
            await ExpectChatMessageAsync(socketB, conversationId, "two", userA, ct);

            // A single Unsubscribe is enough. Any duplicate received meanwhile stays pending and fails the last check
            await UnsubscribeAsync(socketB, conversationId, ct);
            CountSubscribers(conversationId).Should().Be(0);
            pubSubService.ConnectionSubscriptions.ContainsKey(GetConnectionId(userB.Id)).Should().BeFalse();

            await SendChatMessageAsync(socketA, conversationId, "three", ct);
            (await GetMessagesOldestFirstAsync(conversationId, ct)).Select(x => x.Content).Should().Equal("one", "two", "three");

            (await socketB.NoMessageWithinAsync(TimeSpan.FromMilliseconds(500))).Should().BeTrue();
        }

        [Fact]
        public async Task DirectMessages_UserWithTwoConnections_OtherConnectionReceivesTheMessage()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var (_, conversationId) = await MakeFriendsAsync(userA, userB, ct);

            await using var socketA1 = await ConnectAsync(userA, ct);
            await using var socketA2 = await ConnectAsync(userA, ct);
            await using var socketB = await ConnectAsync(userB, ct);
            CountConnections(userA.Id).Should().Be(2);

            await SubscribeAsync(socketA1, conversationId, ct);
            await SubscribeAsync(socketA2, conversationId, ct);
            await SubscribeAsync(socketB, conversationId, ct);
            CountSubscribers(conversationId).Should().Be(3);

            await SendChatMessageAsync(socketA1, conversationId, "from my laptop", ct);

            await ExpectChatMessageAsync(socketB, conversationId, "from my laptop", userA, ct);
            await ExpectChatMessageAsync(socketA2, conversationId, "from my laptop", userA, ct);

            // The sender connection only gets the Ack
            (await socketA1.NoMessageWithinAsync(TimeSpan.FromMilliseconds(300))).Should().BeTrue();
        }

        [Fact]
        public async Task DirectMessages_MessagesOnlyReachTheirConversation()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var userC = await CreateUserAsync("userC1");
            var (_, conversationAB) = await MakeFriendsAsync(userA, userB, ct);
            var (_, conversationAC) = await MakeFriendsAsync(userA, userC, ct);
            conversationAB.Should().NotBe(conversationAC);

            await using var socketA = await ConnectAsync(userA, ct);
            await using var socketB = await ConnectAsync(userB, ct);
            await using var socketC = await ConnectAsync(userC, ct);
            await SubscribeAsync(socketA, conversationAB, ct);
            await SubscribeAsync(socketA, conversationAC, ct);
            await SubscribeAsync(socketB, conversationAB, ct);
            await SubscribeAsync(socketC, conversationAC, ct);

            // A is in both conversations and receives both messages
            await SendChatMessageAsync(socketB, conversationAB, "B to A", ct);
            await ExpectChatMessageAsync(socketA, conversationAB, "B to A", userB, ct);
            await SendChatMessageAsync(socketC, conversationAC, "C to A", ct);
            await ExpectChatMessageAsync(socketA, conversationAC, "C to A", userC, ct);

            // B did not get C's message and C did not get B's: their next message is A's answer
            await SendChatMessageAsync(socketA, conversationAB, "A to B", ct);
            await ExpectChatMessageAsync(socketB, conversationAB, "A to B", userA, ct);
            await SendChatMessageAsync(socketA, conversationAC, "A to C", ct);
            await ExpectChatMessageAsync(socketC, conversationAC, "A to C", userA, ct);

            (await GetMessagesOldestFirstAsync(conversationAB, ct)).Select(x => x.Content).Should().Equal("B to A", "A to B");
            (await GetMessagesOldestFirstAsync(conversationAC, ct)).Select(x => x.Content).Should().Equal("C to A", "A to C");
        }
    }
}
