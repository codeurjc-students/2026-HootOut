namespace HootOut.Contracts.Common.Presence
{
    /// <summary>
    /// Identifies this process among the WebSocket replicas. A new id on every start, so a restarted container
    /// never inherits the connections of its previous life (those are removed by the sweep). Singleton.
    /// </summary>
    public sealed class PresenceInstance
    {
        public Guid Id { get; } = Guid.CreateVersion7();
    }
}
