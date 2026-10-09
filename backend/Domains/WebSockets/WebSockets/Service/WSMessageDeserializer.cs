
using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Entities;
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
        public async Task HandleMessageAsync(Guid connectionId, WSUserInfo user, string rawMessage, CancellationToken ct = default)
        {
            WebSocketMessageDto? messageDto;
            try
            {
                messageDto = JsonConvert.DeserializeObject<WebSocketMessageDto>(rawMessage);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Invalid JSON from connection {ConnectionId} for user {UserId}", connectionId, user.UserId);
                return;
            }

            if (messageDto == null)
            {
                logger.LogWarning("Empty Message from connection {ConnectionId} for user {UserId}", connectionId, user.UserId);
                return;
            }

            if (messageDto.Type == 0)
            {
                logger.LogWarning("Missing Message Type for connection {ConnectionId} and user {UserId}", connectionId, user.UserId);
            }

            try
            {
                var message = new WebSocketMessage
                {
                    Id = messageDto.Id,
                    Channel = messageDto.Channel,
                    Payload = messageDto.Payload,
                    Type = messageDto.Type,
                    ConnectionId = connectionId,
                };

                foreach (var handler in wsMessageHandlers.Where(x => x.Type == message.Type))
                {
                    await handler.HandleMessageAsync(connectionId, user, message, ct);
                }

                if (message.Id.HasValue) //After processing we Ack the message
                {
                    var ackMessage = new WebSocketMessageDto
                    {
                        ReplyTo = message.Id.Value,
                        Type = WSHandlerType.Ack,
                        Channel = message.Channel,
                        Error = message.Error,
                        Payload = message.Payload
                    };
                    await wsMessageSender.SendMessageAsync(connectionId, ackMessage);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Handler failed for {Type}", messageDto.Type);

                if (messageDto.Id.HasValue)
                {
                    await wsMessageSender.SendMessageAsync(connectionId, new WebSocketMessageDto
                    {
                        Type = WSHandlerType.Error,
                        ReplyTo = messageDto.Id,
                        Error = "Could not process message"
                    });
                }

            }
        }
    }
}
