using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Handlers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Net.WebSockets;
using System.Text;

namespace HootOut.HootOutWebsockets.IntegrationTests.Common
{
    public sealed class WSTestClient
    {
        public readonly WebSocket Socket;
        public readonly TimeSpan Timeout;

        // Messages skipped by ReceiveUntilAsync (e.g. broadcasts that arrive while waiting for an Ack).
        // ReceiveAsync returns them first, so no message is lost.
        private readonly Queue<WebSocketMessageDto> pending = new();

        public int PendingCount => pending.Count;

        public static JsonSerializerSettings JsonSettings { get; set; } = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        private WSTestClient(WebSocket socket, TimeSpan timeout)
        {
            this.Socket = socket;
            this.Timeout = timeout;
        }

        public WebSocketState State => Socket.State;

        public static async Task<WSTestClient> ConnectAsync(
        WebApplicationFactory<Program> factory,
        string path = "/ws",
        string wsTicket = "",
        TimeSpan? timeout = null,
        Action<WebSocketClient>? configure = null,
        CancellationToken ct = default)
        {
            var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(5);

            factory.Server.BaseAddress = new Uri("https://localhost");
            var wsClient = factory.Server.CreateWebSocketClient();
            configure?.Invoke(wsClient); // e.g. add headers / subprotocols for auth

            using var cts = CreateCts(effectiveTimeout, ct);
            var Socket = await wsClient.ConnectAsync(new Uri($"wss://localhost{path}?ticket={wsTicket}"), cts.Token);

            return new WSTestClient(Socket, effectiveTimeout);
        }

        public Task SendAsync(WebSocketMessageDto message, CancellationToken ct = default) => SendAsync<WebSocketMessageDto>(message, ct);

        public Task SendAsync<T>(T message, CancellationToken ct = default) => SendRawAsync(JsonConvert.SerializeObject(message, JsonSettings), ct);

        /// <summary>Send an arbitrary string, e.g. malformed JSON to test error handling.</summary>
        public async Task SendRawAsync(string text, CancellationToken ct = default)
        {
            using var cts = CreateCts(Timeout, ct);
            await Socket.SendAsync(
                Encoding.UTF8.GetBytes(text),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cts.Token);
        }

        // ---------- Receive ----------

        public Task<WebSocketMessageDto> ReceiveAsync(CancellationToken ct = default)
            => pending.Count > 0 ? Task.FromResult(pending.Dequeue()) : ReceiveAsync<WebSocketMessageDto>(ct);

        public async Task<T> ReceiveAsync<T>(CancellationToken ct = default)
        {
            var text = await ReceiveTextAsync(ct);
            return JsonConvert.DeserializeObject<T>(text, JsonSettings)
                   ?? throw new InvalidOperationException($"Message deserialized to null: {text}");
        }

        /// <summary>Receive exactly <paramref name="count"/> messages, in order.</summary>
        public async Task<List<WebSocketMessageDto>> ReceiveManyAsync(int count, CancellationToken ct = default)
        {
            var list = new List<WebSocketMessageDto>(count);
            for (var i = 0; i < count; i++)
                list.Add(await ReceiveAsync(ct));
            return list;
        }

        /// <summary>
        /// Keep receiving until a message matches the predicate. The skipped messages are kept
        /// and returned later by <see cref="ReceiveAsync(CancellationToken)"/>, in order.
        /// </summary>
        public async Task<WebSocketMessageDto> ReceiveUntilAsync(
            Func<WebSocketMessageDto, bool> predicate, CancellationToken ct = default)
        {
            // First look in the messages already received
            if (pending.Count > 0)
            {
                var kept = new List<WebSocketMessageDto>(pending.Count);
                WebSocketMessageDto? found = null;
                while (pending.Count > 0)
                {
                    var queued = pending.Dequeue();
                    if (found == null && predicate(queued))
                        found = queued;
                    else
                        kept.Add(queued);
                }
                foreach (var queued in kept)
                    pending.Enqueue(queued);

                if (found != null) return found;
            }

            while (true)
            {
                var msg = await ReceiveAsync<WebSocketMessageDto>(ct);
                if (predicate(msg)) return msg;
                pending.Enqueue(msg);
            }
        }

        /// <summary>Receives one complete text message as a string, reassembling it if it arrives in multiple chunks.</summary>
        public async Task<string> ReceiveTextAsync(CancellationToken ct = default)
        {
            using var cts = CreateCts(Timeout, ct);
            var buffer = new byte[4 * 1024];
            using var ms = new MemoryStream();

            WebSocketReceiveResult result;
            do
            {
                result = await Socket.ReceiveAsync(buffer, cts.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                    throw new InvalidOperationException(
                        $"Server closed the Socket: {result.CloseStatus} ({result.CloseStatusDescription})");

                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
        }

        /// <summary>
        /// Returns true if NO message arrives within the window. Use as the last step of a test:
        /// cancelling a pending receive can leave the Socket in an aborted state.
        /// </summary>
        public async Task<bool> NoMessageWithinAsync(TimeSpan window)
        {
            if (pending.Count > 0)
                return false;

            using var cts = new CancellationTokenSource(window);
            try
            {
                var buffer = new byte[4 * 1024];
                await Socket.ReceiveAsync(buffer, cts.Token);
                return false;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
        }

        /// <summary>Waits for the server to close the connection and returns the close status.</summary>
        public async Task<WebSocketCloseStatus?> WaitForCloseAsync(CancellationToken ct = default)
        {
            using var cts = CreateCts(Timeout, ct);
            var buffer = new byte[1024];

            while (Socket.State is WebSocketState.Open or WebSocketState.CloseSent)
            {
                var result = await Socket.ReceiveAsync(buffer, cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                    return result.CloseStatus;
            }
            return Socket.CloseStatus;
        }

        public async Task<WebSocketMessageDto> SendWithAckAsync(WebSocketMessageDto message, CancellationToken ct = default)
        {
            message.Id ??= Guid.NewGuid();
            await SendAsync(message, ct);

            var reply = await ReceiveUntilAsync(m => m.ReplyTo == message.Id, ct);

            if (reply.Type == WSHandlerType.Error)
                throw new InvalidOperationException($"Server rejected {message.Type}: {reply.Error}");

            return reply;
        }

        /// <summary>Sends the message with an Id and returns the reply (Ack or Error) without throwing.</summary>
        public async Task<WebSocketMessageDto> SendAndGetReplyAsync(WebSocketMessageDto message, CancellationToken ct = default)
        {
            message.Id ??= Guid.NewGuid();
            await SendAsync(message, ct);

            return await ReceiveUntilAsync(m => m.ReplyTo == message.Id, ct);
        }

        // ---------- Close ----------

        public async Task CloseAsync(
            WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure,
            string description = "test done",
            CancellationToken ct = default)
        {
            if (Socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived)) return;

            using var cts = CreateCts(Timeout, ct);
            await Socket.CloseAsync(status, description, cts.Token);
        }

        public async ValueTask DisposeAsync()
        {
            try { await CloseAsync(); }
            catch { /* Socket already gone; nothing to clean up */ }
            Socket.Dispose();
        }

        // ---------- Helpers ----------

        private static CancellationTokenSource CreateCts(TimeSpan timeout, CancellationToken ct)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            return cts;
        }

    }
}
