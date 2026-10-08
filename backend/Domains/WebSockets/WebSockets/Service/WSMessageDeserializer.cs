using HootOut.Contracts.WebSocket;
using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.Contracts.WebSockets.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace HootOut.WebSockets.Service
{
    public class WSMessageDeserializer : IWSMessageDeserializer
    {
        private ILogger<WSMessageDeserializer> logger;
        private IEnumerable<IWSMessageHandler> wsMessageHandlers;

        private IWSMessageSender wsMessageSender;

        public WSMessageDeserializer(ILogger<WSMessageDeserializer> logger,
            IEnumerable<IWSMessageHandler> wsMessageHandlers,
            IWSMessageSender wSMessageSender)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.wsMessageHandlers = wsMessageHandlers ?? throw new ArgumentNullException(nameof(wsMessageHandlers));
            this.wsMessageSender = wSMessageSender ?? throw new ArgumentNullException(nameof(wsMessageSender));
        }
        public async Task HandleMessageAsync(Guid connectionId, WSUserInfo user, string rawMessage)
        {
            WebSocketMessage? message;
            try
            {
                message = JsonConvert.DeserializeObject<WebSocketMessage>(rawMessage);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Invalid JSON from connection {ConnectionId} for user {UserId}", connectionId, user.UserId);
                return;
            }

            if (message == null)
            {
                logger.LogWarning("Empty Message from connection {ConnectionId} for user {UserId}", connectionId, user.UserId);
                return;
            }

            if (message.Type == 0)
            {
                logger.LogWarning("Missing Message Type for connection {ConnectionId} and user {UserId}", connectionId, user.UserId);
            }

            try
            {
                wsMessageHandlers.Where(x => x.Type == message.Type).ToList().ForEach(async x => await x.HandleMessageAsync(connectionId, user, message));

                if (message.Id.HasValue) //After processing we Ack the message
                {
                    message.Type = WSHandlerType.Ack;
                    message.ReplyTo = message.Id;
                    await wsMessageSender.SendMessageAsync(connectionId, message);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Handler failed for {Type}", message.Type);

                if (message.Id.HasValue)
                {
                    await wsMessageSender.SendMessageAsync(connectionId, new WebSocketMessage
                    {
                        Type = WSHandlerType.Error,
                        ReplyTo = message.Id,
                        Error = "Could not process message"
                    });
                }

            }
        }
    }
}
