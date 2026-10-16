using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Admin;
using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Tests.Features.Admin;

public sealed class AuditLogQueryTests
{
    [Fact]
    public async Task A_date_range_covers_whole_tashkent_days()
    {
        var logs = new FakeLogs();

        await new GetAuditLogHandler(logs, new Allow()).Handle(new GetAuditLogQuery(1, 300, new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 12)), CancellationToken.None);

        // Tashkent is UTC+5: 10 Oct starts at 9 Oct 19:00 UTC, the day after 12 Oct starts at 12 Oct 19:00 UTC.
        Assert.Equal(new DateTime(2026, 10, 9, 19, 0, 0, DateTimeKind.Utc), logs.From);
        Assert.Equal(new DateTime(2026, 10, 12, 19, 0, 0, DateTimeKind.Utc), logs.To);
        Assert.Equal(300, logs.Take);
    }

    [Fact]
    public async Task Without_dates_the_newest_entries_come_and_the_page_stays_within_limits()
    {
        var logs = new FakeLogs();

        await new GetAuditLogHandler(logs, new Allow()).Handle(new GetAuditLogQuery(1, 9999), CancellationToken.None);

        Assert.False(logs.RangeAsked);
        Assert.Equal(500, logs.Take);
    }

    [Theory]
    [InlineData(10, 12, true)]
    [InlineData(12, 12, true)]
    [InlineData(12, 10, false)]
    public void The_start_may_not_be_after_the_end(int from, int to, bool valid)
    {
        Assert.Equal(valid, new GetAuditLogValidator().Validate(new GetAuditLogQuery(1, 100, new DateOnly(2026, 10, from), new DateOnly(2026, 10, to))).IsValid);
    }

    private sealed class Allow : IAdminAccessPolicy { public void EnsureAllowed(long telegramUserId) { } }

    private sealed class FakeLogs : IAuditLogRepository
    {
        public DateTime? From { get; private set; }
        public DateTime? To { get; private set; }
        public int Take { get; private set; }
        public bool RangeAsked { get; private set; }
        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken) { Take = take; return Task.FromResult<IReadOnlyList<AuditLog>>([]); }
        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
        {
            (Take, From, To, RangeAsked) = (take, fromUtc, toUtc, true);
            return Task.FromResult<IReadOnlyList<AuditLog>>([]);
        }
    }
}
