using System.Collections.Concurrent;

namespace BloggerBazar.Api.Security;

// Security B4: what the Content-Security-Policy (report-only for now) would block, so the policy can be made strict
// without breaking the app. Memory of this instance only: it empties on a deploy, which is enough for the trial week.
public sealed class CspReportLog
{
    private const int MaxEntries = 200;
    private readonly ConcurrentDictionary<string, CspReportEntry> entries = new();

    public void Add(string? directive, string? blocked, string? source, DateTime utcNow)
    {
        var key = $"{Clip(directive, 64)}|{Clip(blocked, 200)}|{Clip(source, 200)}";
        if (!entries.ContainsKey(key) && entries.Count >= MaxEntries) return;
        entries.AddOrUpdate(key,
            _ => new CspReportEntry(Clip(directive, 64), Clip(blocked, 200), Clip(source, 200), 1, utcNow, utcNow),
            (_, entry) => entry with { Count = entry.Count + 1, LastSeenAtUtc = utcNow });
    }

    public IReadOnlyList<CspReportEntry> Snapshot() =>
        entries.Values.OrderByDescending(entry => entry.LastSeenAtUtc).ToArray();

    // Only where a blocked thing came from is kept (no query strings, which may carry data).
    private static string Clip(string? value, int max)
    {
        var text = (value ?? "").Trim();
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") text = uri.GetLeftPart(UriPartial.Path);
        return text.Length <= max ? text : text[..max];
    }
}

public sealed record CspReportEntry(string Directive, string Blocked, string Source, int Count, DateTime FirstSeenAtUtc, DateTime LastSeenAtUtc);
