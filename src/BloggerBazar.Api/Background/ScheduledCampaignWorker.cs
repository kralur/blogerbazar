using BloggerBazar.Application.Features.Campaigns;
using MediatR;

namespace BloggerBazar.Api.Background;

// D69: every minute publishes the campaigns whose planned time has come.
// Safe on several instances: each publication is a conditional update, so a campaign goes out (and is announced) once.
internal sealed class ScheduledCampaignWorker(IServiceScopeFactory scopeFactory, ILogger<ScheduledCampaignWorker> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

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
            var published = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new PublishScheduledCampaignsCommand(DateTime.UtcNow), stoppingToken);
            if (published > 0)
            {
                logger.LogInformation("Scheduled campaigns published: {Count}", published);
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Scheduled campaign publishing failed");
        }
    }
}
