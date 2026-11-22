using FluentAssertions;
using HootOut.CommonIntegrationTests.Services;
using HootOut.Contracts.Common.Enums;
using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.HootOutWebsockets.IntegrationTests.Common;

namespace HootOut.HootOutWebsockets.IntegrationTests.ServerTests
{
    [Collection("IntegrationTests")]
    public class WSServerMembersTests : WSTestBase, IClassFixture<WebSocketServerFixture>, IClassFixture<TestContaineRabbitConnectionFactory>
    {
        public WSServerMembersTests(WebSocketServerFixture serverFixture, TestContaineRabbitConnectionFactory rabbitConnectionFactory)
            : base(serverFixture, rabbitConnectionFactory)
        {
        }

        [Fact]
        public async Task ServerChannel_FiveMembersTakingTurns_EveryoneReceivesAllMessagesInOrder()
        {
            var ct = TestContext.Current.CancellationToken;
            const int rounds = 3;
            var (users, _, conversationId) = await CreateServerWithMembersAsync(5, ct);
            var sockets = await ConnectAndSubscribeAsync(users, conversationId, ct);
            try
            {
                CountSubscribers(conversationId).Should().Be(users.Count);

                // Each user writes after the previous one got its Ack
                var sent = new List<(TestUser Author, string Content)>();
                for (var round = 1; round <= rounds; round++)
                {
                    for (var i = 0; i < users.Count; i++)
                    {
                        var content = $"round {round} from {users[i].Username}";
                        await SendChatMessageAsync(sockets[i], conversationId, content, ct);
                        sent.Add((users[i], content));
                    }
                }

                // Everyone receives the messages of the others, once and in the order they were sent
                for (var i = 0; i < users.Count; i++)
                {
                    var expected = sent.Where(x => x.Author.Id != users[i].Id).ToList();
                    var received = await sockets[i].ReceiveManyAsync(expected.Count, ct);

                    received.Should().OnlyContain(x => x.Type == WSHandlerType.ChatMessage && x.Channel == conversationId.ToString());
                    received.Select(x => ReadChatMessage(x).Content).Should().Equal(expected.Select(x => x.Content));
                    received.Select(x => ReadChatMessage(x).Author!.Id).Should().Equal(expected.Select(x => x.Author.Id));
                    received.Select(x => ReadChatMessage(x).Author!.Username).Should().Equal(expected.Select(x => x.Author.Username));
                }

                var messages = await GetMessagesOldestFirstAsync(conversationId, ct);
                messages.Select(x => x.Content).Should().Equal(sent.Select(x => x.Content));
                messages.Select(x => x.Author!.Id).Should().Equal(sent.Select(x => x.Author.Id));

                foreach (var socket in sockets)
                {
                    socket.PendingCount.Should().Be(0);
                }
            }
            finally
            {
                await DisposeAllAsync(sockets);
            }
        }

        [Fact]
        public async Task ServerChannel_FiveMembersWritingAtTheSameTime_EveryoneReceivesEverySenderInOrder()
        {
            var ct = TestContext.Current.CancellationToken;
            const int messagesPerUser = 5;
            var (users, _, conversationId) = await CreateServerWithMembersAsync(5, ct);
            var sockets = await ConnectAndSubscribeAsync(users, conversationId, ct);
            try
            {
                string Content(TestUser user, int i) => $"{user.Username} message {i}";

                // Everyone sends all their messages at once, without waiting for the Acks
                await Task.WhenAll(users.Select(async (user, index) =>
                {
                    for (var i = 1; i <= messagesPerUser; i++)
                    {
                        await sockets[index].SendAsync(ChatMessage(conversationId, Content(user, i), Guid.NewGuid()), ct);
                    }
                }));

                var totalPerSocket = messagesPerUser * users.Count; // Own Acks + messages of the other 4 users
                var broadcasts = new List<List<(Guid AuthorId, string? Content)>>();

                for (var index = 0; index < users.Count; index++)
                {
                    var user = users[index];
                    var received = await sockets[index].ReceiveManyAsync(totalPerSocket, ct);

                    // Own Acks, in the order they were sent
                    received.Where(x => x.Type == WSHandlerType.Ack).Select(x => ReadChatMessage(x).Content)
                        .Should().Equal(Enumerable.Range(1, messagesPerUser).Select(i => Content(user, i)));

                    var chatMessages = received.Where(x => x.Type == WSHandlerType.ChatMessage)
                        .Select(x => ReadChatMessage(x))
                        .Select(x => (AuthorId: x.Author!.Id, x.Content))
                        .ToList();
                    chatMessages.Should().HaveCount(messagesPerUser * (users.Count - 1));
                    chatMessages.Should().NotContain(x => x.AuthorId == user.Id);

                    // The messages of each sender arrive in the order they were sent
                    foreach (var sender in users.Where(x => x.Id != user.Id))
                    {
                        chatMessages.Where(x => x.AuthorId == sender.Id).Select(x => x.Content)
                            .Should().Equal(Enumerable.Range(1, messagesPerUser).Select(i => Content(sender, i)));
                    }

                    broadcasts.Add(chatMessages);
                }

                // Every receiver sees the messages in the same global order (the order of the queue)
                for (var a = 0; a < users.Count; a++)
                {
                    for (var b = a + 1; b < users.Count; b++)
                    {
                        var common = broadcasts[a].Intersect(broadcasts[b]).ToHashSet();
                        broadcasts[a].Where(common.Contains).Should().Equal(broadcasts[b].Where(common.Contains));
                    }
                }

                var messages = await GetMessagesOldestFirstAsync(conversationId, ct);
                messages.Should().HaveCount(messagesPerUser * users.Count);
                foreach (var user in users)
                {
                    messages.Where(x => x.Author!.Id == user.Id).Select(x => x.Content)
                        .Should().Equal(Enumerable.Range(1, messagesPerUser).Select(i => Content(user, i)));
                }
            }
            finally
            {
                await DisposeAllAsync(sockets);
            }
        }

        [Fact]
        public async Task ServerChannel_NotAMember_CannotSubscribeOrSend()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner1");
            var outsider = await CreateUserAsync("outsider1");
            var (_, conversationId) = await CreateServerWithChannelAsync(owner, ct);

            await using var ownerSocket = await ConnectAsync(owner, ct);
            await using var outsiderSocket = await ConnectAsync(outsider, ct);
            await SubscribeAsync(ownerSocket, conversationId, ct);

            (await SendExpectingErrorAsync(outsiderSocket, Subscribe(conversationId), ct)).Should().Be(CannotSubscribeError);
            (await SendExpectingErrorAsync(outsiderSocket, ChatMessage(conversationId, "intruder", Guid.NewGuid()), ct)).Should().Be(CannotSendMessageError);
            CountSubscribers(conversationId).Should().Be(1);

            (await GetMessagesOldestFirstAsync(conversationId, ct)).Should().BeEmpty();
            (await ownerSocket.NoMessageWithinAsync(TimeSpan.FromMilliseconds(300))).Should().BeTrue();
        }

        [Fact]
        public async Task ServerChannel_PendingInvitation_CannotSubscribeOrSendUntilAccepted()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner1");
            var invited = await CreateUserAsync("invited1");
            var (serverId, conversationId) = await CreateServerWithChannelAsync(owner, ct);
            var invitationId = await InviteAsync(serverId, owner, invited, ct);

            await using var ownerSocket = await ConnectAsync(owner, ct);
            await using var invitedSocket = await ConnectAsync(invited, ct);
            await SubscribeAsync(ownerSocket, conversationId, ct);

            // Pending: not a member yet
            (await SendExpectingErrorAsync(invitedSocket, Subscribe(conversationId), ct)).Should().Be(CannotSubscribeError);
            (await SendExpectingErrorAsync(invitedSocket, ChatMessage(conversationId, "too early", Guid.NewGuid()), ct)).Should().Be(CannotSendMessageError);

            // Accepted: now it is a member and can talk with the owner
            await UpdateInvitationAsync(invitationId, InvitationStatus.Accepted, invited, ct);

            await SubscribeAsync(invitedSocket, conversationId, ct);
            CountSubscribers(conversationId).Should().Be(2);

            await SendChatMessageAsync(invitedSocket, conversationId, "thanks for the invitation", ct);
            await ExpectChatMessageAsync(ownerSocket, conversationId, "thanks for the invitation", invited, ct);

            await SendChatMessageAsync(ownerSocket, conversationId, "welcome", ct);
            await ExpectChatMessageAsync(invitedSocket, conversationId, "welcome", owner, ct);

            (await GetMessagesOldestFirstAsync(conversationId, ct)).Select(x => x.Content).Should().Equal("thanks for the invitation", "welcome");
        }

        [Fact]
        public async Task ServerChannel_RejectedOrRevokedInvitation_CannotSubscribeOrSend()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner1");
            var rejecter = await CreateUserAsync("rejecter1");
            var revoked = await CreateUserAsync("revoked1");
            var (serverId, conversationId) = await CreateServerWithChannelAsync(owner, ct);

            var rejectedInvitation = await InviteAsync(serverId, owner, rejecter, ct);
            await UpdateInvitationAsync(rejectedInvitation, InvitationStatus.Rejected, rejecter, ct);

            var revokedInvitation = await InviteAsync(serverId, owner, revoked, ct);
            await UpdateInvitationAsync(revokedInvitation, InvitationStatus.Revoked, owner, ct);

            await using var ownerSocket = await ConnectAsync(owner, ct);
            await SubscribeAsync(ownerSocket, conversationId, ct);

            foreach (var user in new[] { rejecter, revoked })
            {
                await using var socket = await ConnectAsync(user, ct);
                (await SendExpectingErrorAsync(socket, Subscribe(conversationId), ct)).Should().Be(CannotSubscribeError);
                (await SendExpectingErrorAsync(socket, ChatMessage(conversationId, "not a member", Guid.NewGuid()), ct)).Should().Be(CannotSendMessageError);
            }

            CountSubscribers(conversationId).Should().Be(1);
            (await GetMessagesOldestFirstAsync(conversationId, ct)).Should().BeEmpty();
            (await ownerSocket.NoMessageWithinAsync(TimeSpan.FromMilliseconds(300))).Should().BeTrue();
        }

        [Fact]
        public async Task ServerChannel_RemovedMember_CannotSendOrSubscribeAnymore()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner1");
            var member = await CreateUserAsync("member1");
            var (serverId, conversationId) = await CreateServerWithChannelAsync(owner, ct);
            await AddMemberAsync(serverId, owner, member, ct);

            await using var ownerSocket = await ConnectAsync(owner, ct);
            await using var memberSocket = await ConnectAsync(member, ct);
            await SubscribeAsync(ownerSocket, conversationId, ct);
            await SubscribeAsync(memberSocket, conversationId, ct);

            await SendChatMessageAsync(memberSocket, conversationId, "still a member", ct);
            await ExpectChatMessageAsync(ownerSocket, conversationId, "still a member", member, ct);

            await RemoveMemberAsync(serverId, member, ct);

            (await SendExpectingErrorAsync(memberSocket, ChatMessage(conversationId, "removed", Guid.NewGuid()), ct)).Should().Be(CannotSendMessageError);

            // A new connection cannot subscribe again
            await using var newMemberSocket = await ConnectAsync(member, ct);
            (await SendExpectingErrorAsync(newMemberSocket, Subscribe(conversationId), ct)).Should().Be(CannotSubscribeError);

            (await GetMessagesOldestFirstAsync(conversationId, ct)).Select(x => x.Content).Should().Equal("still a member");
            (await ownerSocket.NoMessageWithinAsync(TimeSpan.FromMilliseconds(300))).Should().BeTrue();
        }

        [Fact]
        public async Task ServerChannel_MemberOfAnotherServer_CannotUseThisServer()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner1 = await CreateUserAsync("owner1");
            var owner2 = await CreateUserAsync("owner2");
            var (_, conversation1) = await CreateServerWithChannelAsync(owner1, ct, "server 1");
            var (_, conversation2) = await CreateServerWithChannelAsync(owner2, ct, "server 2");

            await using var socket1 = await ConnectAsync(owner1, ct);
            await using var socket2 = await ConnectAsync(owner2, ct);
            await SubscribeAsync(socket1, conversation1, ct);
            await SubscribeAsync(socket2, conversation2, ct);

            (await SendExpectingErrorAsync(socket1, Subscribe(conversation2), ct)).Should().Be(CannotSubscribeError);
            (await SendExpectingErrorAsync(socket1, ChatMessage(conversation2, "wrong server", Guid.NewGuid()), ct)).Should().Be(CannotSendMessageError);

            // Each one can still use its own server
            await SendChatMessageAsync(socket1, conversation1, "server 1 message", ct);
            await SendChatMessageAsync(socket2, conversation2, "server 2 message", ct);

            (await GetMessagesOldestFirstAsync(conversation1, ct)).Select(x => x.Content).Should().Equal("server 1 message");
            (await GetMessagesOldestFirstAsync(conversation2, ct)).Select(x => x.Content).Should().Equal("server 2 message");

            (await socket1.NoMessageWithinAsync(TimeSpan.FromMilliseconds(300))).Should().BeTrue();
            (await socket2.NoMessageWithinAsync(TimeSpan.FromMilliseconds(300))).Should().BeTrue();
        }

        [Fact]
        public async Task ServerChannel_SeveralChannels_MessagesOnlyReachTheirChannel()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner1");
            var member1 = await CreateUserAsync("member1");
            var member2 = await CreateUserAsync("member2");
            var (serverId, general) = await CreateServerWithChannelAsync(owner, ct);
            var random = await CreateChannelAsync(serverId, ct, "random");
            await AddMemberAsync(serverId, owner, member1, ct);
            await AddMemberAsync(serverId, owner, member2, ct);

            await using var ownerSocket = await ConnectAsync(owner, ct);
            await using var socket1 = await ConnectAsync(member1, ct);
            await using var socket2 = await ConnectAsync(member2, ct);

            // The owner is in both channels, each member only in one
            await SubscribeAsync(ownerSocket, general, ct);
            await SubscribeAsync(ownerSocket, random, ct);
            await SubscribeAsync(socket1, general, ct);
            await SubscribeAsync(socket2, random, ct);

            await SendChatMessageAsync(socket1, general, "in general", ct);
            await SendChatMessageAsync(socket2, random, "in random", ct);

            await ExpectChatMessageAsync(ownerSocket, general, "in general", member1, ct);
            await ExpectChatMessageAsync(ownerSocket, random, "in random", member2, ct);

            // Member 1 did not get the message from random, and member 2 did not get the one from general
            await SendChatMessageAsync(ownerSocket, general, "owner in general", ct);
            await SendChatMessageAsync(ownerSocket, random, "owner in random", ct);
            await ExpectChatMessageAsync(socket1, general, "owner in general", owner, ct);
            await ExpectChatMessageAsync(socket2, random, "owner in random", owner, ct);

            (await GetMessagesOldestFirstAsync(general, ct)).Select(x => x.Content).Should().Equal("in general", "owner in general");
            (await GetMessagesOldestFirstAsync(random, ct)).Select(x => x.Content).Should().Equal("in random", "owner in random");
        }

        [Fact]
        public async Task ServerChannel_MemberDisconnects_TheOthersKeepReceiving()
        {
            var ct = TestContext.Current.CancellationToken;
            var (users, _, conversationId) = await CreateServerWithMembersAsync(3, ct);
            var sockets = await ConnectAndSubscribeAsync(users, conversationId, ct);
            try
            {
                CountSubscribers(conversationId).Should().Be(3);

                await sockets[2].DisposeAsync();
                await WaitUntilAsync(() => CountConnections(users[2].Id) == 0 && CountSubscribers(conversationId) == 2);

                await SendChatMessageAsync(sockets[0], conversationId, "who is still here?", ct);
                await ExpectChatMessageAsync(sockets[1], conversationId, "who is still here?", users[0], ct);

                await SendChatMessageAsync(sockets[1], conversationId, "me", ct);
                await ExpectChatMessageAsync(sockets[0], conversationId, "me", users[1], ct);
            }
            finally
            {
                await DisposeAllAsync(sockets);
            }
        }

        private static WebSocketMessageDto Subscribe(Guid conversationId) => new()
        {
            Id = Guid.NewGuid(),
            Type = WSHandlerType.Subscribe,
            Channel = conversationId.ToString()
        };

        /// <summary>The first user is the owner; the rest accept an invitation.</summary>
        private async Task<(List<TestUser> Users, Guid ServerId, Guid ConversationId)> CreateServerWithMembersAsync(int count, CancellationToken ct)
        {
            var users = await CreateUsersAsync(count, "member");
            var (serverId, conversationId) = await CreateServerWithChannelAsync(users[0], ct);

            foreach (var member in users.Skip(1))
            {
                await AddMemberAsync(serverId, users[0], member, ct);
            }

            return (users, serverId, conversationId);
        }

        private async Task<List<WSTestClient>> ConnectAndSubscribeAsync(List<TestUser> users, Guid conversationId, CancellationToken ct)
        {
            var sockets = new List<WSTestClient>(users.Count);
            try
            {
                foreach (var user in users)
                {
                    var socket = await ConnectAsync(user, ct);
                    sockets.Add(socket);
                    await SubscribeAsync(socket, conversationId, ct);
                }
                return sockets;
            }
            catch
            {
                await DisposeAllAsync(sockets);
                throw;
            }
        }

        private static async Task DisposeAllAsync(IEnumerable<WSTestClient> sockets)
        {
            foreach (var socket in sockets)
            {
                await socket.DisposeAsync();
            }
        }
    }
}
