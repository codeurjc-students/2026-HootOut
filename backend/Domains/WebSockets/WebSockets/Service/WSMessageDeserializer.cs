using FluentValidation;
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
            IWSMessageSender wsMessageSender)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.wsMessageHandlers = wsMessageHandlers ?? throw new ArgumentNullException(nameof(wsMessageHandlers));
            this.wsMessageSender = wsMessageSender ?? throw new ArgumentNullException(nameof(wsMessageSender));
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

                var handlers = wsMessageHandlers.Where(x => x.Type == message.Type).ToList();
                if (handlers.Count == 0)
                {
                    // Invalid (0), Ack, Error or an unknown value: nobody handles it
                    throw new ValidationException("Unsupported message type");
                }

                foreach (var handler in handlers)
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
            catch (ValidationException ex)
            {
                // Validation messages are meant for the user, so they are sent back
                logger.LogInformation("Rejected {Type} from connection {ConnectionId} for user {UserId}: {Error}", messageDto.Type, connectionId, user.UserId, ex.Message);
                await SendErrorAsync(connectionId, messageDto, ex.Errors.FirstOrDefault()?.ErrorMessage ?? ex.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Handler failed for {Type}", messageDto.Type);
                await SendErrorAsync(connectionId, messageDto, "Could not process message");
            }
        }

        private async Task SendErrorAsync(Guid connectionId, WebSocketMessageDto messageDto, string error)
        {
            if (!messageDto.Id.HasValue)
                return;

            await wsMessageSender.SendMessageAsync(connectionId, new WebSocketMessageDto
            {
                Type = WSHandlerType.Error,
                ReplyTo = messageDto.Id,
                Channel = messageDto.Channel,
                Error = error
            });
        }
    }
}
