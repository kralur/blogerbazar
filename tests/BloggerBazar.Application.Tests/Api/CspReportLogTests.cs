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

    // Audit 2026-10-22: junk sent once does not lock real reports out; the entry seen longest ago makes room.
    [Fact]
    public void A_new_report_still_gets_in_when_the_list_is_full_of_old_junk()
    {
        var log = new CspReportLog();
        var start = new DateTime(2026, 10, 22, 8, 0, 0, DateTimeKind.Utc);
        for (var index = 0; index < 200; index++) log.Add("junk", $"https://junk{index}.example/", "", start.AddSeconds(index));

        log.Add("script-src-elem", "https://real.example/x.js", "https://app.example/", start.AddHours(1));

        var entries = log.Snapshot();
        Assert.Equal(200, entries.Count);
        Assert.Equal("https://real.example/x.js", entries[0].Blocked);
        Assert.DoesNotContain(entries, entry => entry.Blocked == "https://junk0.example/");
    }
}
