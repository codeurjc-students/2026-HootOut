namespace HootOut.Contracts.Common.Presence
{
    public interface IPresenceStore
    {
        /// <summary>Adds a connection. Returns true if it is the first connection of the user (offline -> online).</summary>
        Task<bool> AddConnectionAsync(Guid userId, Guid connectionId, CancellationToken ct = default);

        /// <summary>Removes a connection. Returns true if it was the last connection of the user (online -> offline).</summary>
        Task<bool> RemoveConnectionAsync(Guid userId, Guid connectionId, CancellationToken ct = default);

        Task<bool> IsOnlineAsync(Guid userId, CancellationToken ct = default);

        /// <summary>Online users among <paramref name="userIds"/>. One SMISMEMBER on the online set per 1000 ids.</summary>
        Task<IReadOnlySet<Guid>> GetOnlineUsersAsync(IEnumerable<Guid> userIds, CancellationToken ct = default);

        /// <summary>Registers this instance and creates its "alive" key. Called once on start.</summary>
        Task RegisterInstanceAsync(CancellationToken ct = default);

        /// <summary>
        /// Renews the "alive" key of this instance. Returns false if the key had expired, which means other
        /// instances may already have removed our connections and they must be added again.
        /// </summary>
        Task<bool> HeartbeatAsync(CancellationToken ct = default);

        /// <summary>
        /// Removes the connections of instances whose "alive" key expired.
        /// Returns the users that became offline because of it.
        /// </summary>
        Task<IReadOnlyCollection<Guid>> SweepDeadInstancesAsync(CancellationToken ct = default);
    }
}
