using HootOut.Contracts.Common.Presence;

namespace HootOut.HootOutWebsockets.Presence
{
    /// <summary>Registers the instance, renews its "alive" key and removes the connections of dead instances.</summary>
    public sealed class PresenceBackgroundService : BackgroundService
    {
        private readonly ILogger<PresenceBackgroundService> logger;
        private readonly IPresenceStore store;
        private readonly PresenceTracker tracker;
        private readonly PresenceOptions options;

        public PresenceBackgroundService(
            ILogger<PresenceBackgroundService> logger,
            IPresenceStore store,
            PresenceTracker tracker,
            PresenceOptions options)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await store.RegisterInstanceAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Valkey not reachable yet: the first heartbeat registers the instance
                logger.LogWarning(ex, "Could not register the presence instance on start");
            }
            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var nextSweep = DateTime.UtcNow + options.SweepInterval;
            using var timer = new PeriodicTimer(options.HeartbeatInterval);

            while (await WaitAsync(timer, stoppingToken))
            {
                try
                {
                    if (!await store.HeartbeatAsync(stoppingToken))
                    {
                        logger.LogWarning("Presence key of this instance had expired, adding the local connections again");
                        await tracker.ResyncAsync(stoppingToken);
                    }

                    if (DateTime.UtcNow >= nextSweep)
                    {
                        nextSweep = DateTime.UtcNow + options.SweepInterval;
                        foreach (var userId in await store.SweepDeadInstancesAsync(stoppingToken))
                        {
                            // The instance is gone: no grace period
                            await tracker.PublishAsync(userId, PresenceStatus.Offline, stoppingToken);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Valkey or RabbitMQ down for a moment: try again on the next tick
                    logger.LogWarning(ex, "Presence heartbeat/sweep failed");
                }
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            // Sockets closed from now on publish offline without grace period.
            // The "alive" key is left to expire: if something was not removed, a sweep of another instance cleans it.
            tracker.BeginShutdown();
            return base.StopAsync(cancellationToken);
        }

        private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
        {
            try
            {
                return await timer.WaitForNextTickAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
