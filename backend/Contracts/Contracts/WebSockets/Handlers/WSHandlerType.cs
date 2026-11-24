namespace HootOut.Contracts.WebSockets.Handlers
{
    public enum WSHandlerType
    {
        Invalid = 0,
        Subscribe = 1,
        Unsubscribe = 2,
        ChatMessage = 3,
        Ack = 4,
        Error = 5,
        /// <summary>Server -> client. A friend went online or offline. Payload: <see cref="Dtos.PresenceChangedPayload"/>.</summary>
        Presence = 6,

        /// <summary>Server -> client, after connecting (friends) and after PresenceSubscribe (server). Payload: <see cref="Dtos.PresenceSnapshotPayload"/>.</summary>
        PresenceSnapshot = 7,

        /// <summary>Client -> server. Channel = serverId. Receive the presence of the members of a server.</summary>
        PresenceSubscribe = 8,

        /// <summary>Client -> server. Channel = serverId.</summary>
        PresenceUnsubscribe = 9,
    }
}