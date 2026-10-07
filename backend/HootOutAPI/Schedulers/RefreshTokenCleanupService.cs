using HootOut.Authentication.Services;

namespace HootOut.HootOutAPI.Schedulers
{
    public class RefreshTokenCleanupService : BackgroundService
    {
        private readonly IRefreshTokenService refreshTokenService;
        private readonly ILogger<RefreshTokenCleanupService> logger;

        public RefreshTokenCleanupService(
            ILogger<RefreshTokenCleanupService> logger,
            IRefreshTokenService refreshTokenService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.refreshTokenService = refreshTokenService ?? throw new ArgumentNullException(nameof(refreshTokenService));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

            do
            {
                try
                {
                    var deleted = await refreshTokenService.DeleteExpiredAsync(stoppingToken);
                    if (deleted > 0)
                        logger.LogInformation("Deleted {Count} expired refresh tokens.", deleted);

                    //await wsTickets.DeleteExpiredAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Refresh token cleanup failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
