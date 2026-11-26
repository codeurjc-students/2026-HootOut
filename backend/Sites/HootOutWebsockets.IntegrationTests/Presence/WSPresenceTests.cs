using FluentAssertions;
using HootOut.CommonIntegrationTests.Services;
using HootOut.Contracts.Common.Presence;
using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.HootOutWebsockets.IntegrationTests.Common;
using HootOut.HootOutWebsockets.Presence;
using System.Diagnostics;

namespace HootOut.HootOutWebsockets.IntegrationTests.Presence
{
    [Collection("IntegrationTests")]
    public class WSPresenceTests : WSTestBase, IClassFixture<WebSocketServerFixture>, IClassFixture<TestContaineRabbitConnectionFactory>
    {
        private static readonly TimeSpan Silence = TimeSpan.FromMilliseconds(300);

        private readonly TimeSpan gracePeriod;

        public WSPresenceTests(WebSocketServerFixture serverFixture, TestContaineRabbitConnectionFactory rabbitConnectionFactory)
            : base(serverFixture, rabbitConnectionFactory)
        {
            gracePeriod = serverFixture.PresenceOptions.OfflineGracePeriod;
        }

        /// <summary>Long enough for an offline after the grace period to have arrived.</summary>
        private TimeSpan AfterGrace => gracePeriod + TimeSpan.FromSeconds(1);

        #region Friends

        [Fact]
        public async Task Presence_NoFriends_EmptySnapshotAndNoPresenceSubscriptions()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");

            var (socketA, snapshot) = await ConnectWithPresenceAsync(userA, ct);
            await using var _ = socketA;

            snapshot.OnlineUserIds.Should().BeEmpty();
            pubSubService.ConnectionSubscriptions.ContainsKey(GetConnectionId(userA.Id)).Should().BeFalse();
            (await presenceStore.IsOnlineAsync(userA.Id, ct)).Should().BeTrue();

            (await socketA.NoMessageWithinAsync(Silence)).Should().BeTrue();
        }

        [Fact]
        public async Task Presence_FriendOffline_SnapshotEmptyButSubscribedToFriend()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            await MakeFriendsAsync(userA, userB, ct);

            var (socketA, snapshot) = await ConnectWithPresenceAsync(userA, ct);
            await using var _ = socketA;

            snapshot.OnlineUserIds.Should().BeEmpty();
            pubSubService.ConnectionSubscriptions[GetConnectionId(userA.Id)].Should().BeEquivalentTo(new[] { FriendChannel(userB) });
        }

        [Fact]
        public async Task Presence_FriendConnects_ReceivesOnlineAndFriendGetsSnapshotWithMe()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            await MakeFriendsAsync(userA, userB, ct);

            var (socketA, _) = await ConnectWithPresenceAsync(userA, ct);
            await using var a = socketA;

            var (socketB, snapshotB) = await ConnectWithPresenceAsync(userB, ct);
            await using var b = socketB;

            snapshotB.OnlineUserIds.Should().Equal(userA.Id);
            await ExpectPresenceAsync(socketA, FriendChannel(userB), userB, PresenceStatus.Online, ct);

            (await socketA.NoMessageWithinAsync(Silence)).Should().BeTrue();
            (await socketB.NoMessageWithinAsync(Silence)).Should().BeTrue();
        }

        [Fact]
        public async Task Presence_SnapshotHasOnlyOnlineFriends()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var online = await CreateUserAsync("online1");
            var offline = await CreateUserAsync("offline1");
            var notFriend = await CreateUserAsync("stranger1");
            await MakeFriendsAsync(userA, online, ct);
            await MakeFriendsAsync(offline, userA, ct);

            var (socketOnline, _) = await ConnectWithPresenceAsync(online, ct);
            await using var o = socketOnline;
            var (socketStranger, _) = await ConnectWithPresenceAsync(notFriend, ct);
            await using var s = socketStranger;

            var (socketA, snapshot) = await ConnectWithPresenceAsync(userA, ct);
            await using var a = socketA;

            snapshot.OnlineUserIds.Should().Equal(online.Id);
        }

        [Fact]
        public async Task Presence_NotFriend_DoesNotReceiveAnything()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var stranger = await CreateUserAsync("stranger1");

            var (socketStranger, _) = await ConnectWithPresenceAsync(stranger, ct);
            await using var s = socketStranger;

            var (socketA, _) = await ConnectWithPresenceAsync(userA, ct);
            await socketA.CloseAsync(ct: ct);
            await socketA.DisposeAsync();
            await WaitUntilOnlineAsync(userA, false);

            (await socketStranger.NoMessageWithinAsync(AfterGrace)).Should().BeTrue();
        }

        [Fact]
        public async Task Presence_PendingFriendRequest_DoesNotReceivePresence()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            await SendFriendRequestAsync(userA, userB, ct);

            var (socketA, _) = await ConnectWithPresenceAsync(userA, ct);
            await using var a = socketA;
            var (socketB, snapshotB) = await ConnectWithPresenceAsync(userB, ct);
            await using var b = socketB;

            snapshotB.OnlineUserIds.Should().BeEmpty();
            (await socketA.NoMessageWithinAsync(Silence)).Should().BeTrue();
        }

        [Fact]
        public async Task Presence_ExFriend_DoesNotReceivePresence()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            var (relationId, _) = await MakeFriendsAsync(userA, userB, ct);
            await DeleteFriendRelationAsync(relationId, ct);

            var (socketA, _) = await ConnectWithPresenceAsync(userA, ct);
            await using var a = socketA;
            var (socketB, snapshotB) = await ConnectWithPresenceAsync(userB, ct);
            await using var b = socketB;

            snapshotB.OnlineUserIds.Should().BeEmpty();
            (await socketA.NoMessageWithinAsync(Silence)).Should().BeTrue();
        }

        [Fact]
        public async Task Presence_SecondConnectionOfFriend_NoSecondOnline()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            await MakeFriendsAsync(userA, userB, ct);

            var (socketA, _) = await ConnectWithPresenceAsync(userA, ct);
            await using var a = socketA;

            var (socketB1, _) = await ConnectWithPresenceAsync(userB, ct);
            await using var b1 = socketB1;
            await ExpectPresenceAsync(socketA, FriendChannel(userB), userB, PresenceStatus.Online, ct);

            // Second tab: it gets the snapshot too, but A is not told again
            var (socketB2, snapshotB2) = await ConnectWithPresenceAsync(userB, ct);
            await using var b2 = socketB2;
            snapshotB2.OnlineUserIds.Should().Equal(userA.Id);

            (await socketA.NoMessageWithinAsync(Silence)).Should().BeTrue();
        }

        [Fact]
        public async Task Presence_FriendDisconnects_OfflineAfterGracePeriod()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            await MakeFriendsAsync(userA, userB, ct);

            var (socketA, _) = await ConnectWithPresenceAsync(userA, ct);
            await using var a = socketA;
            var (socketB, _) = await ConnectWithPresenceAsync(userB, ct);
            await ExpectPresenceAsync(socketA, FriendChannel(userB), userB, PresenceStatus.Online, ct);

            var stopwatch = Stopwatch.StartNew();
            await socketB.CloseAsync(ct: ct);
            await socketB.DisposeAsync();

            await ExpectPresenceAsync(socketA, FriendChannel(userB), userB, PresenceStatus.Offline, ct);
            stopwatch.Elapsed.Should().BeGreaterThanOrEqualTo(gracePeriod - TimeSpan.FromMilliseconds(100));

            (await presenceStore.IsOnlineAsync(userB.Id, ct)).Should().BeFalse();
            (await socketA.NoMessageWithinAsync(Silence)).Should().BeTrue();
        }

        [Fact]
        public async Task Presence_OneOfTwoConnectionsCloses_NoOffline()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            await MakeFriendsAsync(userA, userB, ct);

            var (socketA, _) = await ConnectWithPresenceAsync(userA, ct);
            await using var a = socketA;
            var (socketB1, _) = await ConnectWithPresenceAsync(userB, ct);
            await using var b1 = socketB1;
            var (socketB2, _) = await ConnectWithPresenceAsync(userB, ct);
            await ExpectPresenceAsync(socketA, FriendChannel(userB), userB, PresenceStatus.Online, ct);

            await socketB2.CloseAsync(ct: ct);
            await socketB2.DisposeAsync();
            await WaitUntilAsync(() => CountConnections(userB.Id) == 1);

            (await socketA.NoMessageWithinAsync(AfterGrace)).Should().BeTrue();
            (await presenceStore.IsOnlineAsync(userB.Id, ct)).Should().BeTrue();
        }

        [Fact]
        public async Task Presence_ReconnectInsideGracePeriod_NoOffline()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            await MakeFriendsAsync(userA, userB, ct);

            var (socketA, _) = await ConnectWithPresenceAsync(userA, ct);
            await using var a = socketA;
            var (socketB, _) = await ConnectWithPresenceAsync(userB, ct);
            await ExpectPresenceAsync(socketA, FriendChannel(userB), userB, PresenceStatus.Online, ct);

            // Page reload
            await socketB.CloseAsync(ct: ct);
            await socketB.DisposeAsync();
            await WaitUntilOnlineAsync(userB, false);
            var (socketB2, _) = await ConnectWithPresenceAsync(userB, ct);
            await using var b2 = socketB2;

            // The store went 1 -> 0 -> 1, so a second Online is published (the client ignores it).
            // The Offline is cancelled because the user is online again when the grace period ends.
            await ExpectPresenceAsync(socketA, FriendChannel(userB), userB, PresenceStatus.Online, ct);
            (await socketA.NoMessageWithinAsync(AfterGrace)).Should().BeTrue();

            (await presenceStore.IsOnlineAsync(userB.Id, ct)).Should().BeTrue();
        }

        [Fact]
        public async Task Presence_Disconnect_RemovesPresenceSubscriptions()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var userB = await CreateUserAsync("userB1");
            await MakeFriendsAsync(userA, userB, ct);

            var (socketA, _) = await ConnectWithPresenceAsync(userA, ct);
            CountPresenceSubscribers(FriendChannel(userB)).Should().Be(1);

            await socketA.CloseAsync(ct: ct);
            await socketA.DisposeAsync();

            await WaitUntilAsync(() => connectionManager.Connections.IsEmpty && pubSubService.ConnectionSubscriptions.IsEmpty);
            pubSubService.ChannelSubscribers.ContainsKey(FriendChannel(userB)).Should().BeFalse();
            pubSubService.ChannelExchanges.ContainsKey(FriendChannel(userB)).Should().BeFalse();
        }

        [Fact]
        public async Task Presence_ClientSendsPresenceTypes_Unsupported()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var (socketA, _) = await ConnectWithPresenceAsync(userA, ct);
            await using var a = socketA;

            foreach (var type in new[] { WSHandlerType.Presence, WSHandlerType.PresenceSnapshot })
            {
                (await SendExpectingErrorAsync(socketA, new WebSocketMessageDto
                {
                    Id = Guid.NewGuid(),
                    Type = type,
                    Channel = "friends"
                }, ct)).Should().Be(UnsupportedTypeError);
            }
        }

        #endregion

        #region Servers

        [Fact]
        public async Task ServerPresence_Subscribe_SnapshotWithOnlineMembers()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner1");
            var online = await CreateUserAsync("member1");
            var offline = await CreateUserAsync("member2");
            var stranger = await CreateUserAsync("stranger1");
            var (serverId, _) = await CreateServerWithChannelAsync(owner, ct);
            await AddMemberAsync(serverId, owner, online, ct);
            await AddMemberAsync(serverId, owner, offline, ct);

            var (socketOnline, _) = await ConnectWithPresenceAsync(online, ct);
            await using var o = socketOnline;
            var (socketStranger, _) = await ConnectWithPresenceAsync(stranger, ct);
            await using var s = socketStranger;

            var (socketOwner, _) = await ConnectWithPresenceAsync(owner, ct);
            await using var ow = socketOwner;
            var snapshot = await PresenceSubscribeAsync(socketOwner, serverId, ct);

            // The owner is a member too and is online
            snapshot.OnlineUserIds.Should().BeEquivalentTo(new[] { owner.Id, online.Id });
            pubSubService.ConnectionSubscriptions[GetConnectionId(owner.Id)].Should().BeEquivalentTo(new[] { ServerChannel(serverId) });
        }

        [Fact]
        public async Task ServerPresence_Subscribed_ReceivesOnlineAndOfflineOfMembers()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner1");
            var member = await CreateUserAsync("member1");
            var (serverId, _) = await CreateServerWithChannelAsync(owner, ct);
            await AddMemberAsync(serverId, owner, member, ct);

            var (socketOwner, _) = await ConnectWithPresenceAsync(owner, ct);
            await using var ow = socketOwner;
            await PresenceSubscribeAsync(socketOwner, serverId, ct);

            var (socketMember, _) = await ConnectWithPresenceAsync(member, ct);
            await ExpectPresenceAsync(socketOwner, ServerChannel(serverId), member, PresenceStatus.Online, ct);

            await socketMember.CloseAsync(ct: ct);
            await socketMember.DisposeAsync();
            await ExpectPresenceAsync(socketOwner, ServerChannel(serverId), member, PresenceStatus.Offline, ct);

            (await socketOwner.NoMessageWithinAsync(Silence)).Should().BeTrue();
        }

        [Fact]
        public async Task ServerPresence_NotSubscribed_ReceivesNothing()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner1");
            var member = await CreateUserAsync("member1");
            var (serverId, _) = await CreateServerWithChannelAsync(owner, ct);
            await AddMemberAsync(serverId, owner, member, ct);

            // Subscription is explicit: being a member is not enough
            var (socketOwner, _) = await ConnectWithPresenceAsync(owner, ct);
            await using var ow = socketOwner;
            var (socketMember, _) = await ConnectWithPresenceAsync(member, ct);
            await using var m = socketMember;

            (await socketOwner.NoMessageWithinAsync(Silence)).Should().BeTrue();
        }

        [Fact]
        public async Task ServerPresence_Unsubscribe_StopsReceiving()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner1");
            var member = await CreateUserAsync("member1");
            var (serverId, _) = await CreateServerWithChannelAsync(owner, ct);
            await AddMemberAsync(serverId, owner, member, ct);

            var (socketOwner, _) = await ConnectWithPresenceAsync(owner, ct);
            await using var ow = socketOwner;
            await PresenceSubscribeAsync(socketOwner, serverId, ct);
            CountPresenceSubscribers(ServerChannel(serverId)).Should().Be(1);

            await PresenceUnsubscribeAsync(socketOwner, serverId, ct);
            CountPresenceSubscribers(ServerChannel(serverId)).Should().Be(0);
            pubSubService.ConnectionSubscriptions.ContainsKey(GetConnectionId(owner.Id)).Should().BeFalse();

            var (socketMember, _) = await ConnectWithPresenceAsync(member, ct);
            await using var m = socketMember;

            (await socketOwner.NoMessageWithinAsync(Silence)).Should().BeTrue();
        }

        [Fact]
        public async Task ServerPresence_UnsubscribeNotSubscribed_Ack()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var (socketA, _) = await ConnectWithPresenceAsync(userA, ct);
            await using var a = socketA;

            var ack = await PresenceUnsubscribeAsync(socketA, Guid.NewGuid(), ct);
            ack.Type.Should().Be(WSHandlerType.Ack);
        }

        [Fact]
        public async Task ServerPresence_SubscribeTwice_ReceivesEachChangeOnce()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner1");
            var member = await CreateUserAsync("member1");
            var (serverId, _) = await CreateServerWithChannelAsync(owner, ct);
            await AddMemberAsync(serverId, owner, member, ct);

            var (socketOwner, _) = await ConnectWithPresenceAsync(owner, ct);
            await using var ow = socketOwner;
            await PresenceSubscribeAsync(socketOwner, serverId, ct);
            await PresenceSubscribeAsync(socketOwner, serverId, ct);
            CountPresenceSubscribers(ServerChannel(serverId)).Should().Be(1);

            var (socketMember, _) = await ConnectWithPresenceAsync(member, ct);
            await using var m = socketMember;
            await ExpectPresenceAsync(socketOwner, ServerChannel(serverId), member, PresenceStatus.Online, ct);

            (await socketOwner.NoMessageWithinAsync(Silence)).Should().BeTrue();
        }

        [Fact]
        public async Task ServerPresence_FriendAndMember_ReceivesBothChannels()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner1");
            var member = await CreateUserAsync("member1");
            await MakeFriendsAsync(owner, member, ct);
            var (serverId, _) = await CreateServerWithChannelAsync(owner, ct);
            await AddMemberAsync(serverId, owner, member, ct);

            var (socketOwner, _) = await ConnectWithPresenceAsync(owner, ct);
            await using var ow = socketOwner;
            await PresenceSubscribeAsync(socketOwner, serverId, ct);

            var (socketMember, _) = await ConnectWithPresenceAsync(member, ct);
            await using var m = socketMember;

            // Published on the user channel first, then on each server
            await ExpectPresenceAsync(socketOwner, FriendChannel(member), member, PresenceStatus.Online, ct);
            await ExpectPresenceAsync(socketOwner, ServerChannel(serverId), member, PresenceStatus.Online, ct);

            (await socketOwner.NoMessageWithinAsync(Silence)).Should().BeTrue();
        }

        [Fact]
        public async Task ServerPresence_OnlySubscribedServer()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner1");
            var member = await CreateUserAsync("member1");
            var (server1, _) = await CreateServerWithChannelAsync(owner, ct, "server one");
            var (server2, _) = await CreateServerWithChannelAsync(owner, ct, "server two");
            await AddMemberAsync(server1, owner, member, ct);
            await AddMemberAsync(server2, owner, member, ct);

            var (socketOwner, _) = await ConnectWithPresenceAsync(owner, ct);
            await using var ow = socketOwner;
            await PresenceSubscribeAsync(socketOwner, server2, ct);

            var (socketMember, _) = await ConnectWithPresenceAsync(member, ct);
            await using var m = socketMember;

            await ExpectPresenceAsync(socketOwner, ServerChannel(server2), member, PresenceStatus.Online, ct);
            (await socketOwner.NoMessageWithinAsync(Silence)).Should().BeTrue();
        }

        [Fact]
        public async Task ServerPresence_NotMember_CannotSubscribe()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner1");
            var stranger = await CreateUserAsync("stranger1");
            var (serverId, _) = await CreateServerWithChannelAsync(owner, ct);

            var (socket, _) = await ConnectWithPresenceAsync(stranger, ct);
            await using var s = socket;

            (await SendExpectingErrorAsync(socket, PresenceSubscribe(serverId.ToString()), ct)).Should().Be(CannotSubscribePresenceError);
            CountPresenceSubscribers(ServerChannel(serverId)).Should().Be(0);
            (await socket.NoMessageWithinAsync(Silence)).Should().BeTrue(); // no snapshot
        }

        [Fact]
        public async Task ServerPresence_PendingInvitation_CannotSubscribe()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner1");
            var invited = await CreateUserAsync("invited1");
            var (serverId, _) = await CreateServerWithChannelAsync(owner, ct);
            await InviteAsync(serverId, owner, invited, ct);

            var (socket, _) = await ConnectWithPresenceAsync(invited, ct);
            await using var s = socket;

            (await SendExpectingErrorAsync(socket, PresenceSubscribe(serverId.ToString()), ct)).Should().Be(CannotSubscribePresenceError);
        }

        [Fact]
        public async Task ServerPresence_RemovedMember_CannotSubscribe()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner1");
            var member = await CreateUserAsync("member1");
            var (serverId, _) = await CreateServerWithChannelAsync(owner, ct);
            await AddMemberAsync(serverId, owner, member, ct);
            await RemoveMemberAsync(serverId, member, ct);

            var (socket, _) = await ConnectWithPresenceAsync(member, ct);
            await using var s = socket;

            (await SendExpectingErrorAsync(socket, PresenceSubscribe(serverId.ToString()), ct)).Should().Be(CannotSubscribePresenceError);
        }

        [Fact]
        public async Task ServerPresence_RemovedMember_NotInSnapshotAndNotPublished()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner1");
            var member = await CreateUserAsync("member1");
            var (serverId, _) = await CreateServerWithChannelAsync(owner, ct);
            await AddMemberAsync(serverId, owner, member, ct);
            await RemoveMemberAsync(serverId, member, ct);

            var (socketMember, _) = await ConnectWithPresenceAsync(member, ct);
            await using var m = socketMember;

            var (socketOwner, _) = await ConnectWithPresenceAsync(owner, ct);
            await using var ow = socketOwner;
            var snapshot = await PresenceSubscribeAsync(socketOwner, serverId, ct);
            snapshot.OnlineUserIds.Should().Equal(owner.Id);

            await socketMember.CloseAsync(ct: ct);
            (await socketOwner.NoMessageWithinAsync(AfterGrace)).Should().BeTrue();
        }

        [Fact]
        public async Task ServerPresence_NotExistingServer_CannotSubscribe()
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var (socket, _) = await ConnectWithPresenceAsync(userA, ct);
            await using var s = socket;

            // Same error as not being a member: it does not reveal which servers exist
            (await SendExpectingErrorAsync(socket, PresenceSubscribe(Guid.NewGuid().ToString()), ct)).Should().Be(CannotSubscribePresenceError);
        }

        [Theory]
        [InlineData(null, "Channel is required")]
        [InlineData("", "Channel is required")]
        [InlineData("   ", "Channel is required")]
        [InlineData("foo", "Invalid channel")]
        public async Task ServerPresence_InvalidChannel_Error(string? channel, string error)
        {
            var ct = TestContext.Current.CancellationToken;
            var userA = await CreateUserAsync("userA1");
            var (socket, _) = await ConnectWithPresenceAsync(userA, ct);
            await using var s = socket;

            (await SendExpectingErrorAsync(socket, PresenceSubscribe(channel), ct)).Should().Be(error);

            (await SendExpectingErrorAsync(socket, new WebSocketMessageDto
            {
                Id = Guid.NewGuid(),
                Type = WSHandlerType.PresenceUnsubscribe,
                Channel = channel
            }, ct)).Should().Be(error);

            // The connection stays open
            socket.State.Should().Be(System.Net.WebSockets.WebSocketState.Open);
        }

        [Fact]
        public async Task ServerPresence_ChatSubscriptionAndPresenceSubscription_AreIndependent()
        {
            var ct = TestContext.Current.CancellationToken;
            var owner = await CreateUserAsync("owner1");
            var (serverId, conversationId) = await CreateServerWithChannelAsync(owner, ct);

            var (socketOwner, _) = await ConnectWithPresenceAsync(owner, ct);
            await using var ow = socketOwner;
            await SubscribeAsync(socketOwner, conversationId, ct);
            await PresenceSubscribeAsync(socketOwner, serverId, ct);

            pubSubService.ChannelExchanges[conversationId.ToString()].Should().Be(nameof(WSHandlerType.ChatMessage));
            pubSubService.ChannelExchanges[ServerChannel(serverId)].Should().Be(PresenceTracker.Exchange);

            // Leaving the presence keeps the chat
            await PresenceUnsubscribeAsync(socketOwner, serverId, ct);
            ChatSubscriptions(owner.Id).Should().Equal(conversationId.ToString());
            await SendChatMessageAsync(socketOwner, conversationId, "still here", ct);
        }

        private static WebSocketMessageDto PresenceSubscribe(string? channel) => new()
        {
            Id = Guid.NewGuid(),
            Type = WSHandlerType.PresenceSubscribe,
            Channel = channel
        };

        #endregion
    }
}
