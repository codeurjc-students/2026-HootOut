using HootOut.Contracts.WebSocket;
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

        public static JsonSerializerSettings JsonSettings { get; set; } = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
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
        TimeSpan? timeout = null,
        Action<WebSocketClient>? configure = null,
        CancellationToken ct = default)
        {
            var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(5);

            var wsClient = factory.Server.CreateWebSocketClient();
            configure?.Invoke(wsClient); // e.g. add headers / subprotocols for auth

            using var cts = CreateCts(effectiveTimeout, ct);
            var Socket = await wsClient.ConnectAsync(new Uri($"ws://localhost{path}"), cts.Token);

            return new WSTestClient(Socket, effectiveTimeout);
        }

        public Task SendAsync(WebSocketMessage message, CancellationToken ct = default) => SendAsync<WebSocketMessage>(message, ct);

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

        public Task<WebSocketMessage> ReceiveAsync(CancellationToken ct = default) => ReceiveAsync<WebSocketMessage>(ct);

        public async Task<T> ReceiveAsync<T>(CancellationToken ct = default)
        {
            var text = await ReceiveTextAsync(ct);
            return JsonConvert.DeserializeObject<T>(text, JsonSettings)
                   ?? throw new InvalidOperationException($"Message deserialized to null: {text}");
        }

        /// <summary>Receive exactly <paramref name="count"/> messages, in order.</summary>
        public async Task<List<WebSocketMessage>> ReceiveManyAsync(int count, CancellationToken ct = default)
        {
            var list = new List<WebSocketMessage>(count);
            for (var i = 0; i < count; i++)
                list.Add(await ReceiveAsync(ct));
            return list;
        }

        /// <summary>Keep receiving until a message matches the predicate (skips unrelated broadcasts).</summary>
        public async Task<WebSocketMessage> ReceiveUntilAsync(
            Func<WebSocketMessage, bool> predicate, CancellationToken ct = default)
        {
            while (true)
            {
                var msg = await ReceiveAsync(ct);
                if (predicate(msg)) return msg;
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
