using FluentValidation;
using HootOut.Contracts.WebSockets.Dtos;
using HootOut.Contracts.WebSockets.Entities;
using HootOut.Contracts.WebSockets.Handlers;
using HootOut.Contracts.WebSockets.Services;

namespace HootOut.WebSockets.Handlers
{
    /// <summary>Channel = serverId. The client sends it when it leaves the server view.</summary>
    public class PresenceUnsubscribeHandler : IWSMessageHandler
    {
        public const string MissingChannelMessage = "Channel is required";
        public const string InvalidChannelMessage = "Invalid channel";

        public WSHandlerType Type => WSHandlerType.PresenceUnsubscribe;

        private readonly IPresenceTracker presenceTracker;

        public PresenceUnsubscribeHandler(IPresenceTracker presenceTracker)
        {
            this.presenceTracker = presenceTracker ?? throw new ArgumentNullException(nameof(presenceTracker));
        }

        public Task HandleMessageAsync(Guid connectionId, WSUserInfo user, WebSocketMessage message, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(message.Channel))
                throw new ValidationException(MissingChannelMessage);

            if (!Guid.TryParse(message.Channel, out var serverId))
                throw new ValidationException(InvalidChannelMessage);

            // No permission check: unsubscribing from something the connection is not subscribed to does nothing
            return presenceTracker.UnsubscribeServerAsync(connectionId, serverId, ct);
        }
    }
}
