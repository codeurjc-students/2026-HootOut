using HootOut.Contracts.WebSockets.Dtos;
using HootOut.HootOutWebsockets.Contracts;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace HootOut.HootOutWebsockets.Services
{
    /// <summary>Outgoing messages of one connection and the task that writes them to its socket.</summary>
    public sealed class WSOutboundQueue
    {
        public required Channel<byte[]> Messages { get; init; }
        public required Task Writer { get; init; }

        /// <summary>
        /// Cancels the send in progress when the connection is aborted or removed. Without it a send blocked by a
        /// client that does not read would only end with the SendTimeout, depending on the transport.
        /// Not disposed on purpose: it has no timer, and Cancel can still be called after the writer ends.
        /// </summary>
        public required CancellationTokenSource Cancellation { get; init; }
    }

    public class WSConnectionManager : IWSConnectionManager
    {
        public readonly ConcurrentDictionary<Guid, WSSession> Connections = new();

        // One queue and one writer per connection: sending never waits for another client,
        // and only one SendAsync runs at a time on each socket (it replaces the old ConnectionLock).
        public readonly ConcurrentDictionary<Guid, WSOutboundQueue> OutboundQueues = new();

        /// <summary>Messages waiting to be sent to one client. If it fills up the client is too slow and its socket is aborted.</summary>
        public int QueueCapacity { get; set; } = 256;

        /// <summary>Maximum time to send one message. A client that does not read in this time is aborted.</summary>
        public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(10);

        private readonly ILogger<WSConnectionManager> logger;

        public WSConnectionManager(ILogger<WSConnectionManager> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<Guid> AddConnectionAsync(WebSocket websocket, WSUserInfo user)
        {
            ArgumentNullException.ThrowIfNull(websocket);
            ArgumentNullException.ThrowIfNull(user);

            var connectionId = Guid.CreateVersion7();

            var messages = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(QueueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait // TryWrite returns false when full
            });

            Connections[connectionId] = new WSSession
            {
                User = user,
                Socket = websocket,
            };

            var cancellation = new CancellationTokenSource();
            OutboundQueues[connectionId] = new WSOutboundQueue
            {
                Messages = messages,
                Cancellation = cancellation,
                Writer = Task.Run(() => WriteLoopAsync(connectionId, websocket, messages.Reader, cancellation.Token))
            };

            return Task.FromResult(connectionId);
        }

        public Task<WSSession?> GetWebSocketSessionByConnectionIdAsync(Guid connectionId)
        {
            Connections.TryGetValue(connectionId, out var wsSession);
            return Task.FromResult(wsSession);
        }

        public Task SendMessageAsync(Guid connectionId, WebSocketMessageDto message)
        {
            var serializedMessage = JsonConvert.SerializeObject(message);
            var rawMessage = Encoding.UTF8.GetBytes(serializedMessage);

            return SendMessageAsync(connectionId, rawMessage);
        }

        public Task SendMessageAsync(Guid connectionId, byte[] rawMessage)
        {
            if (!Connections.TryGetValue(connectionId, out var wsSession))
            {
                return Task.CompletedTask;
            }
            return SendMessageAsync(wsSession, connectionId, rawMessage);
        }

        /// <summary>
        /// Queues the message and returns without waiting for it to be sent.
        /// Messages to the same connection keep their order.
        /// </summary>
        public Task SendMessageAsync(WSSession wsSession, Guid connectionId, byte[] rawMessage)
        {
            ArgumentNullException.ThrowIfNull(wsSession);
            ArgumentNullException.ThrowIfNull(rawMessage);

            if (wsSession.Socket.State != WebSocketState.Open || !OutboundQueues.TryGetValue(connectionId, out var queue))
            {
                return Task.CompletedTask;
            }

            if (!queue.Messages.Writer.TryWrite(rawMessage))
            {
                // Full: the client is not reading. Completed: the connection is being removed.
                if (wsSession.Socket.State == WebSocketState.Open)
                {
                    logger.LogWarning("Outbound queue full for connection {ConnectionId}, aborting the socket", connectionId);
                    Abort(queue, wsSession.Socket);
                }
            }

            return Task.CompletedTask;
        }

        public Task RemoveConnectionAsync(Guid connectionId)
        {
            Connections.TryRemove(connectionId, out _);

            if (OutboundQueues.TryRemove(connectionId, out var queue))
            {
                // The writer ends: no more messages are accepted and the send in progress is cancelled
                queue.Messages.Writer.TryComplete();
                queue.Cancellation.Cancel();
            }

            return Task.CompletedTask;
        }

        private async Task WriteLoopAsync(Guid connectionId, WebSocket socket, ChannelReader<byte[]> reader, CancellationToken connectionToken)
        {
            try
            {
                await foreach (var rawMessage in reader.ReadAllAsync(connectionToken))
                {
                    if (socket.State != WebSocketState.Open)
                        break;

                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(connectionToken);
                    timeout.CancelAfter(SendTimeout);
                    await socket.SendAsync(
                        new ArraySegment<byte>(rawMessage),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        timeout.Token);
                }
            }
            catch (OperationCanceledException) when (connectionToken.IsCancellationRequested)
            {
                // Aborted or removed: nothing else to do
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Timeout sending to connection {ConnectionId}, aborting the socket", connectionId);
                socket.Abort();
            }
            catch (Exception ex)
            {
                // The reading side (WSConnectionHandler) sees the broken socket and removes the connection
                logger.LogWarning(ex, "Error sending message to connection {ConnectionId}", connectionId);
                socket.Abort();
            }
        }

        private static void Abort(WSOutboundQueue queue, WebSocket socket)
        {
            queue.Messages.Writer.TryComplete();
            queue.Cancellation.Cancel();
            socket.Abort();
        }
    }
}
