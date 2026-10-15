using BloggerBazar.Api.Security;

namespace BloggerBazar.Application.Tests.Api;

public sealed class CspReportLogTests
{
    [Fact]
    public void Same_violation_is_counted_once_with_its_latest_time()
    {
        var log = new CspReportLog();
        var first = new DateTime(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc);

        log.Add("script-src-elem", "https://evil.example/x.js", "https://app.example/", first);
        log.Add("script-src-elem", "https://evil.example/x.js", "https://app.example/", first.AddMinutes(5));

        var entry = Assert.Single(log.Snapshot());
        Assert.Equal(2, entry.Count);
        Assert.Equal(first, entry.FirstSeenAtUtc);
        Assert.Equal(first.AddMinutes(5), entry.LastSeenAtUtc);
    }

    [Fact]
    public void Query_strings_are_not_kept_because_they_may_carry_data()
    {
        var log = new CspReportLog();

        log.Add("img-src", "https://cdn.example/a.png?token=secret#frag", "https://app.example/#/deal?x=1", DateTime.UtcNow);

        var entry = Assert.Single(log.Snapshot());
        Assert.Equal("https://cdn.example/a.png", entry.Blocked);
        Assert.Equal("https://app.example/", entry.Source);
    }

    [Fact]
    public void The_list_stops_growing_at_two_hundred_kinds()
    {
        var log = new CspReportLog();

        for (var index = 0; index < 250; index++) log.Add("img-src", $"https://site{index}.example/a.png", "", DateTime.UtcNow);

        Assert.Equal(200, log.Snapshot().Count);
    }
}
