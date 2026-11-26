using HootOut.Contracts.Common.Presence;

namespace HootOut.Contracts.WebSockets.Dtos
{
    /// <summary>
    /// A friend changed its status. The client must treat it as idempotent: an "Online" can arrive
    /// for a friend that is already online (reconnection inside the grace period).
    /// </summary>
    public sealed record PresenceChangedPayload
    {
        public Guid UserId { get; init; }
        public PresenceStatus Status { get; init; }
    }

    /// <summary>Friends that are online when the connection opens. The rest are offline.</summary>
    public sealed record PresenceSnapshotPayload
    {
        public IReadOnlyCollection<Guid> OnlineUserIds { get; init; } = [];
    }
}
