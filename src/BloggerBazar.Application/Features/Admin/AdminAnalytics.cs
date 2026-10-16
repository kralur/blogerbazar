using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Admin;

// Web admin analytics (D57, phase 2): read-only numbers grouped in SQL. The server stores UTC; days and months are
// counted in Tashkent time (UTC+5, no daylight saving), the same calendar the owner sees.
public static class AdminAnalytics
{
    public static readonly TimeSpan TashkentOffset = TimeSpan.FromHours(5);
    public const int DefaultDays = 30;
    public const int MaxDays = 366;
    public const int DefaultRetentionDays = 90;
    public const int DefaultMonths = 12;
    public const int MaxMonths = 36;
    public static readonly IReadOnlyList<int> RetentionHorizons = [1, 7, 30];

    public static DateOnly TodayInTashkent(DateTime utcNow) => DateOnly.FromDateTime(utcNow + TashkentOffset);

    // The UTC instant a Tashkent calendar day starts.
    public static DateTime StartUtc(DateOnly day) => DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue) - TashkentOffset, DateTimeKind.Utc);

    public static (DateOnly From, DateOnly To) Resolve(DateOnly? from, DateOnly? to, int defaultDays, DateTime utcNow)
    {
        var end = to ?? TodayInTashkent(utcNow);
        return (from ?? end.AddDays(-(defaultDays - 1)), end);
    }
}

public sealed record AdminDailyPointDto(DateOnly Date, int NewUsers, int LastSeen, int Campaigns, int Applications, int Offers, int Deals, int CompletedDeals);
public sealed record AdminDailyAnalyticsDto(DateOnly From, DateOnly To, IReadOnlyList<AdminDailyPointDto> Days);

// Month is "yyyy-MM"; Average is the mean agreed price of the deals that have one, null when none has.
public sealed record AdminMoneyMonthDto(string Month, int CompletedDeals, int PricedDeals, long Total, long? Average);
public sealed record AdminMoneyAnalyticsDto(IReadOnlyList<AdminMoneyMonthDto> Months);

// Key is a city or category key as stored on profiles (the frontend translates it). Deals count by the creator's
// (blogger or brand face) city and categories; campaigns only once published.
public sealed record AdminGeographyRowDto(string Key, int Bloggers, int BrandFaces, int Campaigns, int Deals);
public sealed record AdminGeographyDto(IReadOnlyList<AdminGeographyRowDto> Cities, IReadOnlyList<AdminGeographyRowDto> Categories);

// Approximate: "returned after N days" means the last app open is at least N days after the first one.
public sealed record AdminRetentionStepDto(int Days, int Eligible, int Returned);
public sealed record AdminRetentionDto(DateOnly From, DateOnly To, int Cohort, IReadOnlyList<AdminRetentionStepDto> Steps);

// Source is the ad label (t.me/bot?start=<label>), null for people who came without one.
public sealed record AdminSourceRowDto(string? Source, int Users, int WithProfile, int WithDeal);
public sealed record AdminSourcesDto(IReadOnlyList<AdminSourceRowDto> Sources);

public sealed record GetAdminDailyAnalyticsQuery(long ActorTelegramUserId, DateOnly? From = null, DateOnly? To = null) : IRequest<AdminDailyAnalyticsDto>;
public sealed record GetAdminMoneyAnalyticsQuery(long ActorTelegramUserId, int Months = AdminAnalytics.DefaultMonths) : IRequest<AdminMoneyAnalyticsDto>;
public sealed record GetAdminGeographyQuery(long ActorTelegramUserId) : IRequest<AdminGeographyDto>;
public sealed record GetAdminRetentionQuery(long ActorTelegramUserId, DateOnly? From = null, DateOnly? To = null) : IRequest<AdminRetentionDto>;
public sealed record GetAdminSourcesQuery(long ActorTelegramUserId) : IRequest<AdminSourcesDto>;

public sealed class GetAdminDailyAnalyticsValidator : AbstractValidator<GetAdminDailyAnalyticsQuery>
{
    public GetAdminDailyAnalyticsValidator()
    {
        RuleFor(query => query).Must(query => AdminAnalyticsRanges.IsValid(query.From, query.To)).WithName("period");
    }
}

public sealed class GetAdminRetentionValidator : AbstractValidator<GetAdminRetentionQuery>
{
    public GetAdminRetentionValidator()
    {
        RuleFor(query => query).Must(query => AdminAnalyticsRanges.IsValid(query.From, query.To)).WithName("period");
    }
}

public sealed class GetAdminMoneyAnalyticsValidator : AbstractValidator<GetAdminMoneyAnalyticsQuery>
{
    public GetAdminMoneyAnalyticsValidator()
    {
        RuleFor(query => query.Months).InclusiveBetween(1, AdminAnalytics.MaxMonths);
    }
}

internal static class AdminAnalyticsRanges
{
    // Both ends optional; when given: from not after to, at most a year, and not after tomorrow.
    public static bool IsValid(DateOnly? from, DateOnly? to)
    {
        var (start, end) = AdminAnalytics.Resolve(from, to, 1, DateTime.UtcNow);
        var latest = AdminAnalytics.TodayInTashkent(DateTime.UtcNow).AddDays(1);
        return start <= end && end.DayNumber - start.DayNumber < AdminAnalytics.MaxDays && end <= latest;
    }
}

public sealed class GetAdminDailyAnalyticsHandler(IAdminAccessPolicy access, IAdminAnalyticsReadModel analytics) : IRequestHandler<GetAdminDailyAnalyticsQuery, AdminDailyAnalyticsDto>
{
    public async Task<AdminDailyAnalyticsDto> Handle(GetAdminDailyAnalyticsQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.ActorTelegramUserId);
        var (from, to) = AdminAnalytics.Resolve(query.From, query.To, AdminAnalytics.DefaultDays, DateTime.UtcNow);
        var counted = (await analytics.GetDailyAsync(from, to, cancellationToken)).ToDictionary(point => point.Date);
        // Every day of the period is present, quiet days as zeros, so a chart never skips a date.
        var days = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1)
            .Select(offset => from.AddDays(offset))
            .Select(day => counted.TryGetValue(day, out var point) ? point : new AdminDailyPointDto(day, 0, 0, 0, 0, 0, 0, 0))
            .ToArray();
        return new AdminDailyAnalyticsDto(from, to, days);
    }
}

public sealed class GetAdminMoneyAnalyticsHandler(IAdminAccessPolicy access, IAdminAnalyticsReadModel analytics) : IRequestHandler<GetAdminMoneyAnalyticsQuery, AdminMoneyAnalyticsDto>
{
    public async Task<AdminMoneyAnalyticsDto> Handle(GetAdminMoneyAnalyticsQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.ActorTelegramUserId);
        var today = AdminAnalytics.TodayInTashkent(DateTime.UtcNow);
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-(query.Months - 1));
        var counted = (await analytics.GetMoneyAsync(first, cancellationToken)).ToDictionary(month => month.Month);
        var months = Enumerable.Range(0, query.Months)
            .Select(offset => first.AddMonths(offset).ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture))
            .Select(month => counted.TryGetValue(month, out var row) ? row : new AdminMoneyMonthDto(month, 0, 0, 0, null))
            .ToArray();
        return new AdminMoneyAnalyticsDto(months);
    }
}

public sealed class GetAdminGeographyHandler(IAdminAccessPolicy access, IAdminAnalyticsReadModel analytics) : IRequestHandler<GetAdminGeographyQuery, AdminGeographyDto>
{
    public async Task<AdminGeographyDto> Handle(GetAdminGeographyQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.ActorTelegramUserId);
        return await analytics.GetGeographyAsync(cancellationToken);
    }
}

public sealed class GetAdminRetentionHandler(IAdminAccessPolicy access, IAdminAnalyticsReadModel analytics) : IRequestHandler<GetAdminRetentionQuery, AdminRetentionDto>
{
    public async Task<AdminRetentionDto> Handle(GetAdminRetentionQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.ActorTelegramUserId);
        var (from, to) = AdminAnalytics.Resolve(query.From, query.To, AdminAnalytics.DefaultRetentionDays, DateTime.UtcNow);
        return await analytics.GetRetentionAsync(from, to, DateTime.UtcNow, cancellationToken);
    }
}

public sealed class GetAdminSourcesHandler(IAdminAccessPolicy access, IAdminAnalyticsReadModel analytics) : IRequestHandler<GetAdminSourcesQuery, AdminSourcesDto>
{
    public async Task<AdminSourcesDto> Handle(GetAdminSourcesQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.ActorTelegramUserId);
        return new AdminSourcesDto(await analytics.GetSourcesAsync(cancellationToken));
    }
}
