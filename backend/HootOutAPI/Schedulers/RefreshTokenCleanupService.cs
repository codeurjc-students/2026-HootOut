using HootOut.Authentication.Services;
using HootOut.Contracts.Authentication.Services;

namespace HootOut.HootOutAPI.Schedulers
{
    public class RefreshTokenCleanupService : BackgroundService
    {
        private readonly ILogger<RefreshTokenCleanupService> logger;
        private readonly IRefreshTokenService refreshTokenService;
        private readonly IWSTicketService wsTicketService;

        public RefreshTokenCleanupService(
            ILogger<RefreshTokenCleanupService> logger,
            IRefreshTokenService refreshTokenService,
            IWSTicketService wsTicketService
            )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.refreshTokenService = refreshTokenService ?? throw new ArgumentNullException(nameof(refreshTokenService));
            this.wsTicketService = wsTicketService ?? throw new ArgumentNullException(nameof(wsTicketService));
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

                    await wsTicketService.DeleteExpiredAsync(stoppingToken);
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
