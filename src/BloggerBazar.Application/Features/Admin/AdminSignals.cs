using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using MediatR;

namespace BloggerBazar.Application.Features.Admin;

// Hints for the owner, never automatic actions (decision D59): patterns that fake accounts, review farming and
// impersonation leave in the data. Each list is short and links to the people; the owner decides what to do.
public static class AdminSignalRules
{
    // A deal completed this soon after it started, with a review, is how a pair farms ratings.
    public const int QuickDealMinutes = 30;
    public const int QuickDealDays = 90;
    // Complaints from this many different people about one target within the window.
    public const int ComplaintReporters = 3;
    public const int ComplaintDays = 30;
    // New accounts from one ad label within one hour, and the share of them that made any profile.
    public const int BurstAccounts = 20;
    public const int BurstLowProfilePercent = 10;
    public const int BurstDays = 7;
    public const int MaxRows = 50;
}

public sealed record AdminSignalPersonDto(long TelegramUserId, string Name);
public sealed record AdminDuplicateHandleDto(string Handle, IReadOnlyList<AdminSignalPersonDto> People);
public sealed record AdminSharedPhoneDto(string Phone, IReadOnlyList<AdminSignalPersonDto> People);
public sealed record AdminQuickDealDto(Guid DealId, string? Title, AdminSignalPersonDto Business, AdminSignalPersonDto Creator, int Minutes, int Reviews, DateTime CompletedAtUtc);
public sealed record AdminComplaintTargetDto(string TargetType, Guid TargetId, string? TargetName, long? OwnerTelegramUserId, int Reporters, DateTime LastAtUtc);
public sealed record AdminSignupBurstDto(string Source, DateTime HourUtc, int Accounts, int WithProfile, bool LowProfiles);
public sealed record AdminSignalRulesDto(int QuickDealMinutes, int QuickDealDays, int ComplaintReporters, int ComplaintDays, int BurstAccounts, int BurstLowProfilePercent, int BurstDays);

public sealed record AdminSignalsDto(
    IReadOnlyList<AdminDuplicateHandleDto> DuplicateHandles,
    IReadOnlyList<AdminSharedPhoneDto> SharedPhones,
    IReadOnlyList<AdminQuickDealDto> QuickDeals,
    IReadOnlyList<AdminComplaintTargetDto> ComplaintTargets,
    IReadOnlyList<AdminSignupBurstDto> SignupBursts,
    AdminSignalRulesDto Rules);

public sealed record GetAdminSignalsQuery(long ActorTelegramUserId) : IRequest<AdminSignalsDto>;

public sealed class GetAdminSignalsHandler(IAdminAccessPolicy access, IAdminSignalsReadModel signals) : IRequestHandler<GetAdminSignalsQuery, AdminSignalsDto>
{
    public async Task<AdminSignalsDto> Handle(GetAdminSignalsQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.ActorTelegramUserId);
        var utcNow = DateTime.UtcNow;
        // One DbContext runs one query at a time, so the lists are read in sequence.
        var handles = await signals.GetDuplicateHandlesAsync(AdminSignalRules.MaxRows, cancellationToken);
        var phones = await signals.GetSharedPhonesAsync(AdminSignalRules.MaxRows, cancellationToken);
        var deals = await signals.GetQuickDealsAsync(utcNow.AddDays(-AdminSignalRules.QuickDealDays), AdminSignalRules.QuickDealMinutes, AdminSignalRules.MaxRows, cancellationToken);
        var complaints = await signals.GetComplaintTargetsAsync(utcNow.AddDays(-AdminSignalRules.ComplaintDays), AdminSignalRules.ComplaintReporters, AdminSignalRules.MaxRows, cancellationToken);
        var bursts = await signals.GetSignupBurstsAsync(utcNow.AddDays(-AdminSignalRules.BurstDays), AdminSignalRules.BurstAccounts, AdminSignalRules.MaxRows, cancellationToken);
        return new AdminSignalsDto(handles, phones, deals, complaints, bursts.Select(burst => burst with { LowProfiles = IsLowProfiles(burst, utcNow) }).ToArray(),
            new AdminSignalRulesDto(AdminSignalRules.QuickDealMinutes, AdminSignalRules.QuickDealDays, AdminSignalRules.ComplaintReporters, AdminSignalRules.ComplaintDays,
                AdminSignalRules.BurstAccounts, AdminSignalRules.BurstLowProfilePercent, AdminSignalRules.BurstDays));
    }

    // People need a little time to fill a profile, so an hour that ended less than an hour ago is not judged yet.
    public static bool IsLowProfiles(AdminSignupBurstDto burst, DateTime utcNow) =>
        burst.HourUtc.AddHours(2) <= utcNow && burst.WithProfile * 100 < burst.Accounts * AdminSignalRules.BurstLowProfilePercent;
}
