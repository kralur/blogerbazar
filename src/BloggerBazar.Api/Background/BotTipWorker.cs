using BloggerBazar.Application.Features.Tips;
using MediatR;

namespace BloggerBazar.Api.Background;

// Hourly: on tip days sends each user at most one bot tip (D54). Safe to run on several instances at once.
internal sealed class BotTipWorker(IServiceScopeFactory scopeFactory, ILogger<BotTipWorker> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

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
            var sent = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProcessBotTipsCommand(DateTime.UtcNow), stoppingToken);
            if (sent > 0)
            {
                logger.LogInformation("Bot tips sent: {Count}", sent);
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Bot tip run failed");
        }
    }
}
