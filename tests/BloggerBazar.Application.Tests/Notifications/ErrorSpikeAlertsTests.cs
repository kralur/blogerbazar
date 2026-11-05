using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Notifications;
using BloggerBazar.Application.Tests.Features.Deals;

namespace BloggerBazar.Application.Tests.Notifications;

public sealed class ErrorSpikeAlertsTests
{
    private static readonly ErrorSpikeOptions Settings = new() { WindowMinutes = 5, ServerErrors = 3, RateLimited = 4, QuietMinutes = 30 };

    [Fact]
    public void Alerts_once_when_server_errors_reach_the_threshold_within_a_window()
    {
        var clock = new Clock(new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero));
        var monitor = new ErrorSpikeMonitor(Settings, clock);

        Assert.Null(monitor.Record(500));
        Assert.Null(monitor.Record(503));
        var alert = monitor.Record(500);
        Assert.Equal(new ErrorSpikeAlert(ErrorSpikeKind.ServerErrors, 3, 5), alert);
        // The same window keeps counting but stays quiet.
        Assert.Null(monitor.Record(500));
    }

    [Fact]
    public void Ordinary_answers_and_client_errors_are_not_counted()
    {
        var monitor = new ErrorSpikeMonitor(Settings, new Clock(DateTimeOffset.UnixEpoch.AddDays(20000)));

        foreach (var status in new[] { 200, 201, 204, 400, 401, 403, 404, 409, 422 })
        {
            for (var index = 0; index < 10; index++) Assert.Null(monitor.Record(status));
        }
    }

    [Fact]
    public void A_new_window_starts_from_zero_and_the_quiet_period_holds_across_windows()
    {
        var clock = new Clock(new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero));
        var monitor = new ErrorSpikeMonitor(Settings, clock);
        Assert.Null(monitor.Record(500));
        Assert.Null(monitor.Record(500));

        clock.Now = clock.Now.AddMinutes(5);
        Assert.Null(monitor.Record(500));
        Assert.Null(monitor.Record(500));
        Assert.NotNull(monitor.Record(500));

        clock.Now = clock.Now.AddMinutes(10);
        for (var index = 0; index < 5; index++) Assert.Null(monitor.Record(500));

        clock.Now = clock.Now.AddMinutes(25);
        Assert.Null(monitor.Record(500));
        Assert.Null(monitor.Record(500));
        Assert.NotNull(monitor.Record(500));
    }

    [Fact]
    public void Rate_limit_refusals_have_their_own_threshold_and_alert()
    {
        var monitor = new ErrorSpikeMonitor(Settings, new Clock(new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero)));

        for (var index = 0; index < 3; index++) Assert.Null(monitor.Record(429));
        Assert.Equal(ErrorSpikeKind.RateLimited, monitor.Record(429)?.Kind);
        Assert.Null(monitor.Record(500));
    }

    [Fact]
    public void Turned_off_it_never_alerts()
    {
        var monitor = new ErrorSpikeMonitor(new ErrorSpikeOptions { Enabled = false, ServerErrors = 1 }, new Clock(DateTimeOffset.UtcNow));

        Assert.Null(monitor.Record(500));
        Assert.Null(monitor.Record(500));
    }

    [Fact]
    public async Task The_alert_goes_to_every_configured_owner_and_nobody_else()
    {
        var bot = new SpyBotClient();

        await new ErrorSpikeAlertSender(bot, new Owners(42, 43)).SendAsync(new ErrorSpikeAlert(ErrorSpikeKind.ServerErrors, 37, 5), CancellationToken.None);

        Assert.Equal([42L, 43L], bot.NotifiedChats);
        Assert.All(bot.Texts, text => Assert.Contains("37", text));
        Assert.Contains("за последние 5 мин сервер ответил ошибкой 37 раз", bot.Texts[0]);
    }

    private sealed class Clock(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Owners(params long[] ids) : IConfiguredOwners
    {
        public bool IsConfiguredOwner(long telegramUserId) => ids.Contains(telegramUserId);
        public IReadOnlyList<long> All => ids;
    }
}
