using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Admin;

namespace BloggerBazar.Application.Tests.Features.Admin;

public sealed class AdminAnalyticsHandlerTests
{
    [Fact]
    public async Task Daily_fills_every_day_of_the_period_with_zeros_where_nothing_happened()
    {
        var analytics = new FakeAnalytics { Daily = [new AdminDailyPointDto(new DateOnly(2025, 3, 2), 5, 4, 3, 2, 1, 1, 1)] };

        var result = await new GetAdminDailyAnalyticsHandler(new AllowAll(), analytics)
            .Handle(new GetAdminDailyAnalyticsQuery(1, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 3)), CancellationToken.None);

        Assert.Equal([new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 2), new DateOnly(2025, 3, 3)], result.Days.Select(day => day.Date));
        Assert.Equal([0, 5, 0], result.Days.Select(day => day.NewUsers));
    }

    [Fact]
    public async Task Daily_defaults_to_the_last_thirty_tashkent_days()
    {
        var result = await new GetAdminDailyAnalyticsHandler(new AllowAll(), new FakeAnalytics())
            .Handle(new GetAdminDailyAnalyticsQuery(1), CancellationToken.None);

        Assert.Equal(30, result.Days.Count);
        Assert.Equal(AdminAnalytics.TodayInTashkent(DateTime.UtcNow), result.To);
    }

    [Fact]
    public async Task Money_lists_every_month_up_to_this_one()
    {
        var today = AdminAnalytics.TodayInTashkent(DateTime.UtcNow);
        var thisMonth = today.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        var analytics = new FakeAnalytics { Money = [new AdminMoneyMonthDto(thisMonth, 3, 2, 300, 150)] };

        var result = await new GetAdminMoneyAnalyticsHandler(new AllowAll(), analytics).Handle(new GetAdminMoneyAnalyticsQuery(1, 3), CancellationToken.None);

        Assert.Equal(3, result.Months.Count);
        Assert.Equal(thisMonth, result.Months[^1].Month);
        Assert.Equal(150, result.Months[^1].Average);
        Assert.Null(result.Months[0].Average);
    }

    [Fact]
    public async Task Every_report_needs_an_admin()
    {
        var deny = new DenyAll();
        var analytics = new FakeAnalytics();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new GetAdminDailyAnalyticsHandler(deny, analytics).Handle(new GetAdminDailyAnalyticsQuery(1), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new GetAdminMoneyAnalyticsHandler(deny, analytics).Handle(new GetAdminMoneyAnalyticsQuery(1), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new GetAdminGeographyHandler(deny, analytics).Handle(new GetAdminGeographyQuery(1), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new GetAdminRetentionHandler(deny, analytics).Handle(new GetAdminRetentionQuery(1), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new GetAdminSourcesHandler(deny, analytics).Handle(new GetAdminSourcesQuery(1), CancellationToken.None));
    }

    [Theory]
    [InlineData("2025-03-10", "2025-03-01", false)]
    [InlineData("2024-01-01", "2025-03-01", false)]
    [InlineData("2025-01-01", "2025-12-31", true)]
    [InlineData(null, null, true)]
    public void Periods_are_ordered_and_at_most_a_year(string? from, string? to, bool valid)
    {
        var query = new GetAdminDailyAnalyticsQuery(1, from is null ? null : DateOnly.Parse(from), to is null ? null : DateOnly.Parse(to));

        Assert.Equal(valid, new GetAdminDailyAnalyticsValidator().Validate(query).IsValid);
    }

    [Fact]
    public void A_tashkent_day_starts_at_19_00_utc_the_day_before()
    {
        Assert.Equal(new DateTime(2025, 3, 9, 19, 0, 0, DateTimeKind.Utc), AdminAnalytics.StartUtc(new DateOnly(2025, 3, 10)));
        Assert.Equal(new DateOnly(2025, 3, 11), AdminAnalytics.TodayInTashkent(new DateTime(2025, 3, 10, 19, 30, 0, DateTimeKind.Utc)));
    }

    private sealed class AllowAll : IAdminAccessPolicy { public void EnsureAllowed(long telegramUserId) { } }
    private sealed class DenyAll : IAdminAccessPolicy { public void EnsureAllowed(long telegramUserId) => throw new UnauthorizedAccessException(); }

    private sealed class FakeAnalytics : IAdminAnalyticsReadModel
    {
        public IReadOnlyList<AdminDailyPointDto> Daily { get; init; } = [];
        public IReadOnlyList<AdminMoneyMonthDto> Money { get; init; } = [];
        public Task<IReadOnlyList<AdminDailyPointDto>> GetDailyAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) => Task.FromResult(Daily);
        public Task<IReadOnlyList<AdminMoneyMonthDto>> GetMoneyAsync(DateOnly firstMonth, CancellationToken cancellationToken) => Task.FromResult(Money);
        public Task<AdminGeographyDto> GetGeographyAsync(CancellationToken cancellationToken) => Task.FromResult(new AdminGeographyDto([], []));
        public Task<AdminRetentionDto> GetRetentionAsync(DateOnly from, DateOnly to, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new AdminRetentionDto(from, to, 0, []));
        public Task<IReadOnlyList<AdminSourceRowDto>> GetSourcesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AdminSourceRowDto>>([]);
    }
}
