using FluentValidation;
using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Entities;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.Contracts.WebSockets.Services;
using Microsoft.Extensions.Logging;

namespace HootOut.WebSockets.Handlers
{
    /// <summary>
    /// Channel = serverId. Subscribes to the presence of the members of the server and sends a PresenceSnapshot
    /// with Channel = serverId before the Ack. The client sends it when it opens a server.
    /// </summary>
    public class PresenceSubscribeHandler : IWSMessageHandler
    {
        public const string MissingChannelMessage = "Channel is required";
        public const string InvalidChannelMessage = "Invalid channel";
        public const string CannotSubscribeMessage = "Cannot subscribe to this server";

        public WSHandlerType Type => WSHandlerType.PresenceSubscribe;

        private readonly ILogger<PresenceSubscribeHandler> logger;
        private readonly IPresenceTracker presenceTracker;

        public PresenceSubscribeHandler(ILogger<PresenceSubscribeHandler> logger, IPresenceTracker presenceTracker)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.presenceTracker = presenceTracker ?? throw new ArgumentNullException(nameof(presenceTracker));
        }

        public async Task HandleMessageAsync(Guid connectionId, WSUserInfo user, WebSocketMessage message, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(message.Channel))
                throw new ValidationException(MissingChannelMessage);

            if (!Guid.TryParse(message.Channel, out var serverId))
                throw new ValidationException(InvalidChannelMessage);

            try
            {
                await presenceTracker.SubscribeServerAsync(user, connectionId, serverId, ct);
            }
            catch (ValidationException)
            {
                // Same message whether the server does not exist or the user is not a member
                logger.LogWarning("User {UserId} cannot subscribe to the presence of server {ServerId}", user.UserId, serverId);
                throw new ValidationException(CannotSubscribeMessage);
            }
        }
    }
}
