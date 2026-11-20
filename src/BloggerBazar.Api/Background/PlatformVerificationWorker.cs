using BloggerBazar.Application.Features.Bloggers;
using MediatR;

namespace BloggerBazar.Api.Background;

// D68: every six hours reads again the verified Telegram / YouTube numbers older than a week (a batch at a time).
// Safe on several instances: the same channel read twice gives the same number.
internal sealed class PlatformVerificationWorker(IServiceScopeFactory scopeFactory, ILogger<PlatformVerificationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            using var timer = new PeriodicTimer(Interval);
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var changed = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RefreshVerifiedPlatformsCommand(), stoppingToken);
            if (changed > 0)
            {
                logger.LogInformation("Verified platforms refreshed: {Count}", changed);
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Platform verification refresh failed");
        }
    }
}
