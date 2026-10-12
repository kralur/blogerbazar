using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Admin;

// Admin funnel without an events table: the people who first opened the app in the period, and how many of them
// reached each step since then, read from the rows each step leaves behind (profiles, applications, offers, deals,
// reviews). A person counts once per step, and every step is a subset of the one before it.
public static class AdminFunnel
{
    public const string Week = "7d";
    public const string Month = "30d";
    public const string AllTime = "all";
    public static readonly IReadOnlyList<string> Periods = [Week, Month, AllTime];

    // Step keys (D26): fixed camelCase strings, the frontend translates them.
    public const string Opened = "opened";
    public const string RoleSelected = "roleSelected";
    public const string Profile = "profile";
    // A blogger or brand face applied to a campaign or received an offer.
    public const string Reached = "reached";
    // A business published a campaign or sent an offer.
    public const string Posted = "posted";
    // A business received an application or an accepted offer.
    public const string Answered = "answered";
    public const string Deal = "deal";
    public const string Completed = "completed";
    public const string Reviewed = "reviewed";

    public static DateTime? StartOf(string period, DateTime nowUtc) => period switch
    {
        Week => nowUtc.AddDays(-7),
        Month => nowUtc.AddDays(-30),
        _ => null
    };
}

public sealed record AdminFunnelStepDto(string Key, int Count);

// Role: "blogger", "brandFace" or "business".
public sealed record AdminFunnelRoleDto(string Role, IReadOnlyList<AdminFunnelStepDto> Steps);

// FromUtc is null for all time.
public sealed record AdminFunnelDto(string Period, DateTime? FromUtc, IReadOnlyList<AdminFunnelRoleDto> Roles);

public sealed record GetAdminFunnelQuery(long TelegramUserId, string? Period) : IRequest<AdminFunnelDto>;

public sealed class GetAdminFunnelValidator : AbstractValidator<GetAdminFunnelQuery>
{
    public GetAdminFunnelValidator()
    {
        RuleFor(query => query.Period).Must(period => period is null || AdminFunnel.Periods.Contains(period));
    }
}

public sealed class GetAdminFunnelHandler(IAdminAccessPolicy access, IAdminMarketplaceReadModel marketplace) : IRequestHandler<GetAdminFunnelQuery, AdminFunnelDto>
{
    public async Task<AdminFunnelDto> Handle(GetAdminFunnelQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.TelegramUserId);
        var period = query.Period ?? AdminFunnel.Month;
        var fromUtc = AdminFunnel.StartOf(period, DateTime.UtcNow);
        return new AdminFunnelDto(period, fromUtc, await marketplace.GetFunnelAsync(fromUtc, cancellationToken));
    }
}
