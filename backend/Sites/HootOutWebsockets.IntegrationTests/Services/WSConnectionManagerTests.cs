using FluentAssertions;
using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.HootOutWebsockets.IntegrationTests.Common;
using HootOut.HootOutWebsockets.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using System.Net.WebSockets;
using System.Text;

namespace HootOut.HootOutWebsockets.IntegrationTests.Services
{
    public class WSConnectionManagerTests : IDisposable
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        private readonly WSConnectionManager manager = new(NullLogger<WSConnectionManager>.Instance);
        private readonly List<InMemoryWebSocketPair> pairs = new();

        public void Dispose()
        {
            foreach (var pair in pairs)
            {
                pair.Dispose();
            }
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void Constructor_NullLogger_Throws()
        {
            FluentActions.Invoking(() => new WSConnectionManager(null!))
                .Should().Throw<ArgumentNullException>().WithParameterName("logger");
        }

        [Fact]
        public async Task AddConnection_RegistersSessionAndOutboundQueue()
        {
            var pair = NewPair();
            var user = User("testuser");

            var connectionId = await manager.AddConnectionAsync(pair.Server, user);

            connectionId.Should().NotBeEmpty();
            manager.Connections.Should().ContainKey(connectionId);
            manager.OutboundQueues.Should().ContainKey(connectionId);

            var session = await manager.GetWebSocketSessionByConnectionIdAsync(connectionId);
            session.Should().NotBeNull();
            session!.User.Should().Be(user);
            session.Socket.Should().BeSameAs(pair.Server);
        }

        [Fact]
        public async Task AddConnection_NullArguments_Throws()
        {
            await FluentActions.Awaiting(() => manager.AddConnectionAsync(null!, User("testuser")))
                .Should().ThrowAsync<ArgumentNullException>();
            await FluentActions.Awaiting(() => manager.AddConnectionAsync(NewPair().Server, null!))
                .Should().ThrowAsync<ArgumentNullException>();

            manager.Connections.Should().BeEmpty();
            manager.OutboundQueues.Should().BeEmpty();
        }

        [Fact]
        public async Task AddConnection_ManyAtTheSameTime_UniqueIds()
        {
            const int count = 100;
            var sockets = Enumerable.Range(0, count).Select(_ => NewPair().Server).ToList();

            var ids = await Task.WhenAll(sockets.Select((socket, i) => Task.Run(() => manager.AddConnectionAsync(socket, User($"user{i}")))));

            ids.Should().OnlyHaveUniqueItems();
            manager.Connections.Should().HaveCount(count);
            manager.OutboundQueues.Should().HaveCount(count);
        }

        [Fact]
        public async Task GetSession_UnknownConnection_ReturnsNull()
        {
            (await manager.GetWebSocketSessionByConnectionIdAsync(Guid.NewGuid())).Should().BeNull();
        }

        [Fact]
        public async Task SendMessage_Dto_ClientReceivesTheSerializedMessage()
        {
            var ct = TestContext.Current.CancellationToken;
            var pair = NewPair();
            var connectionId = await manager.AddConnectionAsync(pair.Server, User("testuser"));
            var message = new WebSocketMessageDto
            {
                ReplyTo = Guid.NewGuid(),
                Type = WSHandlerType.Ack,
                Channel = "foo",
                Payload = "bar"
            };

            await manager.SendMessageAsync(connectionId, message);

            var text = await pair.ClientReceiveTextAsync(ct).WaitAsync(Timeout, ct);
            var received = JsonConvert.DeserializeObject<WebSocketMessageDto>(text!);
            received.Should().BeEquivalentTo(message);
        }

        [Fact]
        public async Task SendMessage_Sequential_KeepsTheOrder()
        {
            var ct = TestContext.Current.CancellationToken;
            var pair = NewPair();
            var connectionId = await manager.AddConnectionAsync(pair.Server, User("testuser"));
            var messages = Enumerable.Range(1, 100).Select(i => $"message {i}").ToList();

            foreach (var message in messages)
            {
                await manager.SendMessageAsync(connectionId, Encoding.UTF8.GetBytes(message));
            }

            var received = new List<string>();
            for (var i = 0; i < messages.Count; i++)
            {
                received.Add((await pair.ClientReceiveTextAsync(ct).WaitAsync(Timeout, ct))!);
            }
            received.Should().Equal(messages);
        }

        [Fact]
        public async Task SendMessage_ManyThreadsAtTheSameTime_MessagesAreNotMixed()
        {
            var ct = TestContext.Current.CancellationToken;
            const int count = 200;
            var pair = NewPair();
            var connectionId = await manager.AddConnectionAsync(pair.Server, User("testuser"));

            // Big messages (several frames of the pipe) sent from many threads
            var messages = Enumerable.Range(1, count).Select(i => $"{i}:" + new string((char)('a' + i % 26), 3000)).ToList();
            await Task.WhenAll(messages.Select(message => Task.Run(() => manager.SendMessageAsync(connectionId, Encoding.UTF8.GetBytes(message)))));

            var received = new List<string>();
            for (var i = 0; i < count; i++)
            {
                received.Add((await pair.ClientReceiveTextAsync(ct).WaitAsync(Timeout, ct))!);
            }

            // Every message arrives complete and only once (the order between threads is not defined)
            received.Should().BeEquivalentTo(messages);
        }

        [Fact]
        public async Task SendMessage_UnknownConnection_DoesNothing()
        {
            await FluentActions.Awaiting(() => manager.SendMessageAsync(Guid.NewGuid(), Encoding.UTF8.GetBytes("foo")))
                .Should().NotThrowAsync();
            await FluentActions.Awaiting(() => manager.SendMessageAsync(Guid.NewGuid(), new WebSocketMessageDto { Type = WSHandlerType.Ack }))
                .Should().NotThrowAsync();
        }

        [Fact]
        public async Task SendMessage_SocketNotOpen_DoesNothing()
        {
            var pair = NewPair();
            var connectionId = await manager.AddConnectionAsync(pair.Server, User("testuser"));
            pair.Server.Abort();

            await FluentActions.Awaiting(() => manager.SendMessageAsync(connectionId, Encoding.UTF8.GetBytes("foo")))
                .Should().NotThrowAsync();

            manager.OutboundQueues[connectionId].Messages.Reader.Count.Should().Be(0);
        }

        [Fact]
        public async Task RemoveConnection_LeavesNothingBehind()
        {
            var ct = TestContext.Current.CancellationToken;
            var pair = NewPair();
            var connectionId = await manager.AddConnectionAsync(pair.Server, User("testuser"));
            var writer = manager.OutboundQueues[connectionId].Writer;

            await manager.RemoveConnectionAsync(connectionId);

            manager.Connections.Should().BeEmpty();
            manager.OutboundQueues.Should().BeEmpty();
            (await manager.GetWebSocketSessionByConnectionIdAsync(connectionId)).Should().BeNull();

            // The writer task ends: no task left running for a removed connection
            await writer.WaitAsync(Timeout, ct);
        }

        [Fact]
        public async Task RemoveConnection_UnknownOrTwice_DoesNothing()
        {
            var connectionId = await manager.AddConnectionAsync(NewPair().Server, User("testuser"));
            var other = await manager.AddConnectionAsync(NewPair().Server, User("testuser2"));

            await manager.RemoveConnectionAsync(Guid.NewGuid());
            await manager.RemoveConnectionAsync(connectionId);
            await manager.RemoveConnectionAsync(connectionId);

            manager.Connections.Keys.Should().Equal(other);
            manager.OutboundQueues.Keys.Should().Equal(other);
        }

        [Fact]
        public async Task RemoveConnection_ThenSend_NothingIsSent()
        {
            var ct = TestContext.Current.CancellationToken;
            var pair = NewPair();
            var connectionId = await manager.AddConnectionAsync(pair.Server, User("testuser"));
            await manager.RemoveConnectionAsync(connectionId);

            await manager.SendMessageAsync(connectionId, Encoding.UTF8.GetBytes("too late"));

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            await FluentActions.Awaiting(() => pair.ClientReceiveTextAsync(cts.Token))
                .Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task RemoveConnection_WithQueuedMessages_WriterEnds()
        {
            var ct = TestContext.Current.CancellationToken;
            // The client never reads, so the messages stay in the queue
            var pair = NewPair(clientReadBuffer: 1024);
            var connectionId = await manager.AddConnectionAsync(pair.Server, User("testuser"));
            var writer = manager.OutboundQueues[connectionId].Writer;
            for (var i = 0; i < 5; i++)
            {
                await manager.SendMessageAsync(connectionId, new byte[2048]);
            }

            // What the controller does when the connection ends: the socket is gone and the connection is removed
            pair.Server.Abort();
            await manager.RemoveConnectionAsync(connectionId);

            await writer.WaitAsync(Timeout, ct);
            manager.Connections.Should().BeEmpty();
            manager.OutboundQueues.Should().BeEmpty();
        }

        [Fact]
        public async Task AddAndRemoveManyConnections_AtTheSameTime_NoLeftovers()
        {
            var ct = TestContext.Current.CancellationToken;
            const int count = 100;

            var writers = await Task.WhenAll(Enumerable.Range(0, count).Select(i => Task.Run(async () =>
            {
                var connectionId = await manager.AddConnectionAsync(NewPairThreadSafe().Server, User($"user{i}"));
                await manager.SendMessageAsync(connectionId, Encoding.UTF8.GetBytes("hello"));
                var writer = manager.OutboundQueues[connectionId].Writer;
                await manager.RemoveConnectionAsync(connectionId);
                return writer;
            })));

            manager.Connections.Should().BeEmpty();
            manager.OutboundQueues.Should().BeEmpty();
            await Task.WhenAll(writers).WaitAsync(Timeout, ct);
        }

        [Fact]
        public async Task SlowClient_QueueFull_OnlyThatSocketIsAborted()
        {
            var ct = TestContext.Current.CancellationToken;
            manager.SendTimeout = TimeSpan.FromMinutes(1); // Not the timeout: the queue fills first

            // The capacity is read when the connection is added: only the slow one gets a tiny queue
            manager.QueueCapacity = 2;
            var slow = NewPair(clientReadBuffer: 1024); // Never reads
            var slowId = await manager.AddConnectionAsync(slow.Server, User("slowuser"));

            manager.QueueCapacity = 256;
            var fast = NewPair();
            var fastId = await manager.AddConnectionAsync(fast.Server, User("fastuser"));

            // Broadcast: the first message blocks the slow socket, two more fill its queue, the next one aborts it.
            // Meanwhile the fast client keeps getting its messages.
            for (var i = 0; i < 10; i++)
            {
                var sending = manager.SendMessageAsync(slowId, new byte[4096]);
                sending.IsCompleted.Should().BeTrue("sending only queues the message, it never waits for the client");

                await manager.SendMessageAsync(fastId, Encoding.UTF8.GetBytes($"message {i}"));
                (await fast.ClientReceiveTextAsync(ct).WaitAsync(Timeout, ct)).Should().Be($"message {i}");
            }

            await WaitUntilAsync(() => slow.Server.State == WebSocketState.Aborted);
            fast.Server.State.Should().Be(WebSocketState.Open);
            await manager.OutboundQueues[slowId].Writer.WaitAsync(Timeout, ct);
        }

        [Fact]
        public async Task SlowClient_SendTimeout_SocketIsAborted()
        {
            var ct = TestContext.Current.CancellationToken;
            manager.SendTimeout = TimeSpan.FromMilliseconds(200);

            var slow = NewPair(clientReadBuffer: 1024); // Never reads
            var slowId = await manager.AddConnectionAsync(slow.Server, User("slowuser"));

            await manager.SendMessageAsync(slowId, new byte[8192]);

            await WaitUntilAsync(() => slow.Server.State == WebSocketState.Aborted);
            await manager.OutboundQueues[slowId].Writer.WaitAsync(Timeout, ct);
        }

        [Fact]
        public async Task BrokenSocket_DoesNotAffectOtherConnections()
        {
            var ct = TestContext.Current.CancellationToken;
            var broken = NewPair();
            var healthy = NewPair();
            var brokenId = await manager.AddConnectionAsync(broken.Server, User("testuser1"));
            var healthyId = await manager.AddConnectionAsync(healthy.Server, User("testuser2"));

            broken.DropClient();

            for (var i = 0; i < 5; i++)
            {
                await manager.SendMessageAsync(brokenId, Encoding.UTF8.GetBytes($"lost {i}"));
                await manager.SendMessageAsync(healthyId, Encoding.UTF8.GetBytes($"message {i}"));
            }

            for (var i = 0; i < 5; i++)
            {
                (await healthy.ClientReceiveTextAsync(ct).WaitAsync(Timeout, ct)).Should().Be($"message {i}");
            }
            healthy.Server.State.Should().Be(WebSocketState.Open);
        }

        private InMemoryWebSocketPair NewPair(long? clientReadBuffer = null)
        {
            var pair = new InMemoryWebSocketPair(clientReadBuffer);
            pairs.Add(pair);
            return pair;
        }

        private readonly object pairsLock = new();

        private InMemoryWebSocketPair NewPairThreadSafe()
        {
            var pair = new InMemoryWebSocketPair();
            lock (pairsLock)
            {
                pairs.Add(pair);
            }
            return pair;
        }

        private static WSUserInfo User(string username) => new() { UserId = Guid.NewGuid(), Username = username };

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var limit = DateTime.UtcNow + Timeout;
            while (!condition())
            {
                if (DateTime.UtcNow > limit)
                    throw new TimeoutException("The condition was not met in time");
                await Task.Delay(10);
            }
        }
    }
}
