using FluentAssertions;
using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Services;
using HootOut.HootOutWebsockets.IntegrationTests.Common;
using HootOut.HootOutWebsockets.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace HootOut.HootOutWebsockets.IntegrationTests.Services
{
    public class WSConnectionHandlerTests : IDisposable
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        private readonly Mock<IWSMessageDeserializer> deserializer = new(MockBehavior.Strict);
        private readonly ConcurrentQueue<string> received = new();
        private readonly WSConnectionHandler handler;
        private readonly InMemoryWebSocketPair pair = new();
        private readonly Guid connectionId = Guid.NewGuid();
        private readonly WSUserInfo user = new() { UserId = Guid.NewGuid(), Username = "testuser" };

        public WSConnectionHandlerTests()
        {
            deserializer
                .Setup(x => x.HandleMessageAsync(connectionId, user, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, WSUserInfo, string, CancellationToken>((_, _, message, _) => received.Enqueue(message))
                .Returns(Task.CompletedTask);

            handler = new WSConnectionHandler(NullLogger<WSConnectionHandler>.Instance, deserializer.Object);
        }

        public void Dispose()
        {
            pair.Dispose();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void Constructor_NullArguments_Throws()
        {
            FluentActions.Invoking(() => new WSConnectionHandler(null!, deserializer.Object))
                .Should().Throw<ArgumentNullException>().WithParameterName("logger");
            FluentActions.Invoking(() => new WSConnectionHandler(NullLogger<WSConnectionHandler>.Instance, null!))
                .Should().Throw<ArgumentNullException>().WithParameterName("wsMessageDeserializer");
        }

        [Fact]
        public async Task TextMessage_DeliveredOnceToTheDeserializer()
        {
            var ct = TestContext.Current.CancellationToken;
            var reading = StartReading();

            await pair.ClientSendTextAsync("""{"type":1,"channel":"foo"}""", ct: ct);
            await WaitForMessagesAsync(1);

            received.Should().Equal("""{"type":1,"channel":"foo"}""");
            await CloseFromClientAsync(reading, ct);
            deserializer.Verify(x => x.HandleMessageAsync(connectionId, user, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SeveralMessages_DeliveredInOrder()
        {
            var ct = TestContext.Current.CancellationToken;
            var reading = StartReading();

            var messages = Enumerable.Range(1, 50).Select(i => $"message {i}").ToList();
            foreach (var message in messages)
            {
                await pair.ClientSendTextAsync(message, ct: ct);
            }
            await WaitForMessagesAsync(messages.Count);

            received.Should().Equal(messages);
            await CloseFromClientAsync(reading, ct);
        }

        [Fact]
        public async Task FragmentedMessage_ReassembledBeforeDelivering()
        {
            var ct = TestContext.Current.CancellationToken;
            var reading = StartReading();

            await pair.ClientSendTextAsync("first ", endOfMessage: false, ct: ct);
            await pair.ClientSendTextAsync("second ", endOfMessage: false, ct: ct);
            await pair.ClientSendTextAsync("third", endOfMessage: true, ct: ct);
            await pair.ClientSendTextAsync("next message", ct: ct);
            await WaitForMessagesAsync(2);

            received.Should().Equal("first second third", "next message");
            await CloseFromClientAsync(reading, ct);
        }

        [Fact]
        public async Task MessageBiggerThanTheReceiveBuffer_Reassembled()
        {
            var ct = TestContext.Current.CancellationToken;
            var reading = StartReading();
            var message = string.Concat(Enumerable.Range(0, 2000).Select(i => $"{i:D9},")); // 20 000 bytes, 5 receive buffers

            await pair.ClientSendTextAsync(message, ct: ct);
            await WaitForMessagesAsync(1);

            received.Single().Should().Be(message);
            await CloseFromClientAsync(reading, ct);
        }

        [Fact]
        public async Task MultiByteCharacters_SplitBetweenReceiveBuffers_ArriveIntact()
        {
            var ct = TestContext.Current.CancellationToken;
            var reading = StartReading();

            // The 4 KB receive buffer ends in the middle of 'ñ' (2 bytes), '€' (3 bytes) and '🦉' (4 bytes)
            var message1 = new string('a', 4095) + "ñ" + new string('b', 10);
            var message2 = new string('a', 4094) + "€" + new string('b', 10);
            var message3 = new string('a', 4093) + "🦉" + new string('b', 10);
            var message4 = string.Concat(Enumerable.Repeat("ñandú €uro 🦉 ", 2000));

            foreach (var message in new[] { message1, message2, message3, message4 })
            {
                await pair.ClientSendTextAsync(message, ct: ct);
            }
            await WaitForMessagesAsync(4);

            received.Should().Equal(message1, message2, message3, message4);
            await CloseFromClientAsync(reading, ct);
        }

        [Fact]
        public async Task MultiByteCharacter_SplitBetweenFrames_ArrivesIntact()
        {
            var ct = TestContext.Current.CancellationToken;
            var reading = StartReading();
            var owl = Encoding.UTF8.GetBytes("🦉"); // 4 bytes

            await pair.ClientSendBytesAsync(Encoding.UTF8.GetBytes("owl: ").Concat(owl[..2]).ToArray(), WebSocketMessageType.Text, endOfMessage: false, ct);
            await pair.ClientSendBytesAsync(owl[2..], WebSocketMessageType.Text, endOfMessage: true, ct);
            await WaitForMessagesAsync(1);

            received.Single().Should().Be("owl: 🦉");
            await CloseFromClientAsync(reading, ct);
        }

        [Fact]
        public async Task EmptyTextMessage_DeliveredAsEmptyString()
        {
            var ct = TestContext.Current.CancellationToken;
            var reading = StartReading();

            await pair.ClientSendTextAsync("", ct: ct);
            await WaitForMessagesAsync(1);

            received.Single().Should().BeEmpty();
            await CloseFromClientAsync(reading, ct);
        }

        [Fact]
        public async Task MessageOfMaxSize_Accepted()
        {
            var ct = TestContext.Current.CancellationToken;
            var reading = StartReading();
            var message = new string('a', WSConnectionHandler.MaxMessageSize);

            await pair.ClientSendTextAsync(message, ct: ct);
            await WaitForMessagesAsync(1);

            received.Single().Should().HaveLength(WSConnectionHandler.MaxMessageSize);
            await CloseFromClientAsync(reading, ct);
        }

        [Fact]
        public async Task MessageBiggerThanMaxSize_ClosesWithMessageTooBig()
        {
            var ct = TestContext.Current.CancellationToken;
            var reading = StartReading();

            await pair.ClientSendTextAsync(new string('a', WSConnectionHandler.MaxMessageSize + 1), ct: ct);

            (await pair.ClientReceiveCloseAsync(ct)).Should().Be(WebSocketCloseStatus.MessageTooBig);
            await reading.WaitAsync(Timeout, ct);
            received.Should().BeEmpty();
        }

        [Fact]
        public async Task EndlessFragments_ClosedWhenTheLimitIsReached()
        {
            var ct = TestContext.Current.CancellationToken;
            var reading = StartReading();
            var chunk = new string('a', 4096);

            // Never sends EndOfMessage: the server must not keep growing the buffer
            var sending = Task.Run(async () =>
            {
                try
                {
                    for (var i = 0; i < 1000; i++)
                    {
                        await pair.ClientSendTextAsync(chunk, endOfMessage: false, ct: ct);
                    }
                }
                catch (Exception ex) when (ex is WebSocketException or InvalidOperationException or IOException)
                {
                    // Expected: the server closes the connection while the client is still sending
                }
            }, ct);

            (await pair.ClientReceiveCloseAsync(ct)).Should().Be(WebSocketCloseStatus.MessageTooBig);
            await reading.WaitAsync(Timeout, ct);
            await sending.WaitAsync(Timeout, ct);
            received.Should().BeEmpty();
        }

        [Fact]
        public async Task BinaryMessage_ClosesWithInvalidMessageType()
        {
            var ct = TestContext.Current.CancellationToken;
            var reading = StartReading();

            await pair.ClientSendBytesAsync(new byte[] { 1, 2, 3 }, WebSocketMessageType.Binary, ct: ct);

            (await pair.ClientReceiveCloseAsync(ct)).Should().Be(WebSocketCloseStatus.InvalidMessageType);
            await reading.WaitAsync(Timeout, ct);
            pair.Server.State.Should().Be(WebSocketState.Closed);
            received.Should().BeEmpty();
        }

        [Fact]
        public async Task InvalidUtf8_ConnectionEndsWithoutDelivering()
        {
            var ct = TestContext.Current.CancellationToken;
            var reading = StartReading();

            await pair.ClientSendBytesAsync(new byte[] { 0x7B, 0xFF, 0xFE, 0x7D }, WebSocketMessageType.Text, ct: ct);

            // Depending on the runtime, the WebSocket itself or the handler rejects it with InvalidPayloadData
            WebSocketCloseStatus? status = null;
            try
            {
                status = await pair.ClientReceiveCloseAsync(ct);
            }
            catch (WebSocketException)
            {
                // Connection aborted instead of closed: also valid
            }

            await reading.WaitAsync(Timeout, ct);
            if (status != null)
                status.Should().Be(WebSocketCloseStatus.InvalidPayloadData);
            received.Should().BeEmpty();
        }

        [Fact]
        public async Task ClientCloses_ReadingEndsWithoutDelivering()
        {
            var ct = TestContext.Current.CancellationToken;
            var reading = StartReading();

            await CloseFromClientAsync(reading, ct);

            pair.Server.State.Should().Be(WebSocketState.Closed);
            pair.Server.CloseStatus.Should().Be(WebSocketCloseStatus.NormalClosure);
            received.Should().BeEmpty();
        }

        [Fact]
        public async Task ClientDisconnectsWithoutClosing_ReadingEndsWithoutThrowing()
        {
            var ct = TestContext.Current.CancellationToken;
            var reading = StartReading();
            await pair.ClientSendTextAsync("before", ct: ct);
            await WaitForMessagesAsync(1);

            pair.DropClient();

            await FluentActions.Awaiting(() => reading.WaitAsync(Timeout, ct)).Should().NotThrowAsync();
            received.Should().Equal("before");
        }

        [Fact]
        public async Task ServerSocketAborted_ReadingEndsWithoutThrowing()
        {
            var ct = TestContext.Current.CancellationToken;
            var reading = StartReading();

            // What WSConnectionManager does with a slow client
            pair.Server.Abort();

            await FluentActions.Awaiting(() => reading.WaitAsync(Timeout, ct)).Should().NotThrowAsync();
            pair.Server.State.Should().Be(WebSocketState.Aborted);
        }

        [Fact]
        public async Task ClientIgnoresTheCloseHandshake_ServerAbortsAfterTheTimeout()
        {
            var ct = TestContext.Current.CancellationToken;
            var reading = StartReading();

            // The server closes because of the binary message, but the client never answers
            await pair.ClientSendBytesAsync(new byte[] { 1 }, WebSocketMessageType.Binary, ct: ct);

            await reading.WaitAsync(WSConnectionHandler.CloseTimeout + Timeout, ct);
            pair.Server.State.Should().BeOneOf(WebSocketState.Aborted, WebSocketState.Closed);
        }

        [Fact]
        public async Task DeserializerThrows_ExceptionPropagatesToTheCaller()
        {
            var ct = TestContext.Current.CancellationToken;
            deserializer.Reset();
            deserializer
                .Setup(x => x.HandleMessageAsync(connectionId, user, "boom", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));
            var reading = StartReading();

            await pair.ClientSendTextAsync("boom", ct: ct);

            // The controller catches it and removes the connection in its finally
            await FluentActions.Awaiting(() => reading.WaitAsync(Timeout, ct))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        }

        [Fact]
        public async Task SocketNotOpen_ReturnsImmediately()
        {
            var ct = TestContext.Current.CancellationToken;
            pair.Server.Abort();

            await handler.ReadMessagesAsync(pair.Server, connectionId, user).WaitAsync(Timeout, ct);

            received.Should().BeEmpty();
        }

        private Task StartReading() => Task.Run(() => handler.ReadMessagesAsync(pair.Server, connectionId, user));

        private async Task WaitForMessagesAsync(int count)
        {
            var limit = DateTime.UtcNow + Timeout;
            while (received.Count < count)
            {
                if (DateTime.UtcNow > limit)
                    throw new TimeoutException($"Expected {count} messages, received {received.Count}");
                await Task.Delay(10);
            }
        }

        private async Task CloseFromClientAsync(Task reading, CancellationToken ct)
        {
            await pair.Client.CloseAsync(WebSocketCloseStatus.NormalClosure, "test done", ct).WaitAsync(Timeout, ct);
            await reading.WaitAsync(Timeout, ct);
            pair.Client.State.Should().Be(WebSocketState.Closed);
        }
    }
}
