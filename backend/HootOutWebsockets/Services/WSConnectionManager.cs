using HootOut.Contracts.WebSocket;
using HootOut.Contracts.WebSockets.Dtos;
using HootOut.HootOutWebsockets.Contracts;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace HootOut.HootOutWebsockets.Services
{

    public class WSConnectionManager : IWSConnectionManager
    {
        public readonly ConcurrentDictionary<Guid, WSSession> Connections = new();

        public readonly ConcurrentDictionary<Guid, SemaphoreSlim> ConnectionLock = new();

        private readonly ILogger<WSConnectionManager> logger;

        public WSConnectionManager(ILogger<WSConnectionManager> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Guid> AddConnectionAsync(WebSocket websocket, WSUserInfo user)
        {
            var connectionId = Guid.CreateVersion7();

            Connections.TryAdd(connectionId, new WSSession
            {
                User = user, 
                Socket = websocket,
            });
            ConnectionLock.TryAdd(connectionId, new SemaphoreSlim(1, 1));

            logger.LogInformation("WebSocket connected: {ConnectionId} for user: {UserId}. Connections count {ConnectionCount}", connectionId, user.UserId, Connections.Count);

            return connectionId;
        }

        public async Task<WSSession?> GetWebSocketSessionByConnectionIdAsync(Guid connectionId)
        {
            WSSession? wsSession;
            if (Connections.TryGetValue(connectionId, out wsSession))
            {
                return wsSession;
            }
            return wsSession;
        }

        public async Task SendMessageAsync(Guid connectionId, WebSocketMessage message)
        {
            var wsSession = await GetWebSocketSessionByConnectionIdAsync(connectionId);

            if (wsSession == null)
            {
                return;
            }
            await SendMessageAsync(wsSession, connectionId, message);
        }

        public async Task SendMessageAsync(WSSession wsSession, Guid connectionId, WebSocketMessage message)
        {
            try
            {
                if (wsSession.Socket.State == WebSocketState.Open)
                {
                    var rawMessage = JsonConvert.SerializeObject(message);
                    var bytes = Encoding.UTF8.GetBytes(rawMessage);

                    ConnectionLock.TryGetValue(connectionId, out var sendLock);

                    if (sendLock != null)
                    {
                        await sendLock.WaitAsync();

                        try
                        {
                            await wsSession.Socket.SendAsync(
                                new ArraySegment<byte>(bytes),
                                WebSocketMessageType.Text,
                                endOfMessage: true,
                                CancellationToken.None
                            );
                        }
                        finally { sendLock?.Release(); }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error sending message to connection {ConnectionId}", connectionId);
                throw;
            }
        }

        public async Task RemoveConnectionAsync(Guid connectionId)
        {
            Connections.TryRemove(connectionId, out _);
            Connections.TryRemove(connectionId, out _);
            logger.LogInformation("WebSocket disconnected: {ConnectionId}. Connections count {ConnectionCount}", connectionId, Connections.Count);
        }
    }
}
