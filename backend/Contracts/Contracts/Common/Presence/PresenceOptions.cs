namespace HootOut.Contracts.Common.Presence
{
    public sealed class PresenceOptions
    {
        /// <summary>Lifetime of the "alive" key of an instance. If it expires the instance is considered dead.</summary>
        public TimeSpan InstanceTtl { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>How often the instance renews its "alive" key. Must be well below <see cref="InstanceTtl"/>.</summary>
        public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>How often the instance looks for dead instances and removes their connections.</summary>
        public TimeSpan SweepInterval { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Time to wait after the last connection of a user closes before announcing it as offline.
        /// A page reload or a short network cut does not produce an offline/online flicker.
        /// </summary>
        public TimeSpan OfflineGracePeriod { get; set; } = TimeSpan.FromSeconds(5);
    }
}
