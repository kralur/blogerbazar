namespace BloggerBazar.Application.Features.Reviews;

// Reviews open when a deal completes and close 14 days later; a hidden review is published at the latest when the window ends.
public static class ReviewWindow
{
    public static readonly TimeSpan Length = TimeSpan.FromDays(14);

    public static DateTime? EndsAtUtc(DateTime? completedAtUtc) => completedAtUtc + Length;

    public static bool IsOpen(DateTime? completedAtUtc, DateTime nowUtc) => completedAtUtc is { } completed && nowUtc < completed + Length;
}
