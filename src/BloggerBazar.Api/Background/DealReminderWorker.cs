using BloggerBazar.Application.Features.Deals;
using MediatR;

namespace BloggerBazar.Api.Background;

// Hourly: publishes revealed blind reviews and sends due deal reminders. Safe to run on several instances at once.
internal sealed class DealReminderWorker(IServiceScopeFactory scopeFactory, ILogger<DealReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
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
            var sent = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProcessDealRemindersCommand(DateTime.UtcNow), stoppingToken);
            if (sent > 0)
            {
                logger.LogInformation("Deal reminders sent: {Count}", sent);
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Deal reminder run failed");
        }
    }
}
