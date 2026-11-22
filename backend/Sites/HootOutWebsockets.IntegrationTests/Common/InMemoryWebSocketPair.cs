using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text;

namespace HootOut.HootOutWebsockets.IntegrationTests.Common
{
    /// <summary>
    /// Two real WebSockets (server and client) connected in memory with two pipes.
    /// They speak the real protocol (frames, fragments, close handshake) without network or HTTP server.
    /// </summary>
    public sealed class InMemoryWebSocketPair : IDisposable
    {
        public WebSocket Server { get; }
        public WebSocket Client { get; }

        private readonly DuplexPipeStream serverStream;
        private readonly DuplexPipeStream clientStream;

        /// <param name="clientReadBuffer">
        /// Bytes the server can write before the client reads them. Small values simulate a slow client.
        /// </param>
        public InMemoryWebSocketPair(long? clientReadBuffer = null)
        {
            var toServer = new Pipe();
            var toClient = clientReadBuffer.HasValue
                ? new Pipe(new PipeOptions(pauseWriterThreshold: clientReadBuffer.Value, resumeWriterThreshold: clientReadBuffer.Value / 2))
                : new Pipe();

            serverStream = new DuplexPipeStream(toServer.Reader, toClient.Writer);
            clientStream = new DuplexPipeStream(toClient.Reader, toServer.Writer);

            Server = WebSocket.CreateFromStream(serverStream, new WebSocketCreationOptions { IsServer = true, KeepAliveInterval = TimeSpan.Zero });
            Client = WebSocket.CreateFromStream(clientStream, new WebSocketCreationOptions { IsServer = false, KeepAliveInterval = TimeSpan.Zero });
        }

        /// <summary>Cuts the connection from the client side without a close handshake.</summary>
        public void DropClient()
        {
            Client.Abort();
            clientStream.Dispose();
        }

        public void Dispose()
        {
            Server.Dispose();
            Client.Dispose();
            serverStream.Dispose();
            clientStream.Dispose();
        }

        // ---------- Client helpers ----------

        public Task ClientSendTextAsync(string text, bool endOfMessage = true, CancellationToken ct = default)
            => Client.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, endOfMessage, ct);

        public Task ClientSendBytesAsync(byte[] bytes, WebSocketMessageType type, bool endOfMessage = true, CancellationToken ct = default)
            => Client.SendAsync(bytes, type, endOfMessage, ct);

        /// <summary>Receives one complete message on the client. Returns null if it is a close frame.</summary>
        public async Task<string?> ClientReceiveTextAsync(CancellationToken ct = default)
        {
            var buffer = new byte[4 * 1024];
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await Client.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    return null;
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
        }

        /// <summary>Waits for the server close frame, answers it and returns its status.</summary>
        public async Task<WebSocketCloseStatus?> ClientReceiveCloseAsync(CancellationToken ct = default)
        {
            var buffer = new byte[4 * 1024];
            while (true)
            {
                var result = await Client.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await Client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", ct);
                    return result.CloseStatus;
                }
            }
        }

        private sealed class DuplexPipeStream : Stream
        {
            private readonly Stream input;
            private readonly Stream output;

            public DuplexPipeStream(PipeReader reader, PipeWriter writer)
            {
                input = reader.AsStream();
                output = writer.AsStream();
            }

            public override bool CanRead => true;
            public override bool CanWrite => true;
            public override bool CanSeek => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override int Read(byte[] buffer, int offset, int count) => input.Read(buffer, offset, count);
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => input.ReadAsync(buffer, cancellationToken);
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => input.ReadAsync(buffer, offset, count, cancellationToken);

            public override void Write(byte[] buffer, int offset, int count) => output.Write(buffer, offset, count);
            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => output.WriteAsync(buffer, cancellationToken);
            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => output.WriteAsync(buffer, offset, count, cancellationToken);

            public override void Flush() => output.Flush();
            public override Task FlushAsync(CancellationToken cancellationToken) => output.FlushAsync(cancellationToken);
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    // Completes both pipes: the other side reads end of stream
                    input.Dispose();
                    output.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}
