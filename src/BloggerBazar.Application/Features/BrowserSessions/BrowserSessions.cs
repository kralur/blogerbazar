using System.Globalization;
using System.Security.Authentication;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Notifications;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.BrowserSessions;

// D64 stage B: company work in an ordinary browser. Only an active person whose selected role is Business (rule 1)
// and who is an active member of a company signs in, and the same is checked again on every browser request.

// Browser and system as D26 strings, read from the browser's User-Agent; informative only (a browser can claim
// anything), the clients show them so a person recognises their own sign-in.
public static class BrowserDevices
{
    public const string Other = "other";

    public static BrowserDevice FromUserAgent(string? userAgent)
    {
        var agent = userAgent ?? string.Empty;
        bool Has(string value) => agent.Contains(value, StringComparison.OrdinalIgnoreCase);
        var browser = Has("YaBrowser/") ? "yandex"
            : Has("Edg/") || Has("EdgA/") || Has("EdgiOS/") ? "edge"
            : Has("OPR/") || Has("Opera") ? "opera"
            : Has("Firefox/") || Has("FxiOS/") ? "firefox"
            : Has("Chrome/") || Has("CriOS/") || Has("Chromium/") ? "chrome"
            : Has("Safari/") ? "safari"
            : Other;
        var os = Has("Windows") ? "windows"
            : Has("Android") ? "android"
            : Has("iPhone") || Has("iPad") || Has("iPod") ? "ios"
            : Has("CrOS") ? "chromeos"
            : Has("Macintosh") || Has("Mac OS X") ? "macos"
            : Has("Linux") ? "linux"
            : Other;
        return new BrowserDevice(browser, os);
    }
}

public static class BrowserSessionCodes
{
    // The person's selected role is not Business: switch to it in the Mini App first.
    public const string BusinessRoleRequired = "browser_business_role_required";
    // The person is not an active member of a company.
    public const string CompanyRequired = "browser_company_required";
    // The number chosen in the Mini App is not the one the browser shows; the sign-in is cancelled.
    public const string NumberMismatch = "browser_number_mismatch";
}

// Whether this person may sign in to a browser now, as a D26 string.
public static class BrowserEligibility
{
    public const string Ok = "ok";
    public const string SwitchToBusiness = "switchToBusiness";
    public const string NoCompany = "noCompany";

    public static async Task<(string Eligibility, BusinessMembership? Membership)> CheckAsync(PlatformUser user, IBusinessProfileRepository businesses, CancellationToken cancellationToken)
    {
        if (user.IsBlocked || user.IsDeleted)
        {
            return (NoCompany, null);
        }

        var membership = await businesses.GetMembershipAsync(user.TelegramUserId, cancellationToken);
        if (membership is null)
        {
            return (NoCompany, null);
        }

        return user.SelectedMarketplaceRole == MarketplaceRole.Business ? (Ok, membership) : (SwitchToBusiness, membership);
    }
}

public static class BrowserSessionAuditActions
{
    public const string SignedIn = "web.signed-in";
    public const string SignedOut = "web.signed-out";
    public const string SignedOutEverywhere = "web.signed-out-everywhere";
}

public sealed record BrowserSignInStarted(string Code, string Secret, int Number, DateTime ExpiresAtUtc);

public sealed record BrowserSignInPreviewDto(
    string Browser,
    string Os,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    IReadOnlyList<int> Options,
    string Eligibility,
    string? CompanyName);

// Status: one of BrowserSignInStatuses. Ticket only when the browser has just been signed in.
public sealed record BrowserSignInCheck(string Status, BrowserSessionTicket? Ticket, string? FirstName);

public sealed record BrowserSessionDto(
    string Id,
    string Browser,
    string Os,
    DateTime CreatedAtUtc,
    DateTime LastSeenAtUtc,
    DateTime ExpiresAtUtc,
    bool IsCurrent)
{
    public static BrowserSessionDto From(BrowserSession session, string? currentSessionId) => new(
        session.Id,
        session.Device.Browser,
        session.Device.Os,
        session.CreatedAtUtc,
        session.LastSeenAtUtc,
        session.ExpiresAtUtc,
        string.Equals(session.Id, currentSessionId, StringComparison.Ordinal));
}

// The person behind a browser request, as the API hands it to the controllers.
public sealed record BrowserSessionIdentity(BrowserSession Session, PlatformUser User);

public sealed record StartBrowserSignInCommand(BrowserDevice Device) : IRequest<BrowserSignInStarted>;
public sealed record PreviewBrowserSignInQuery(long TelegramUserId, string Code) : IRequest<BrowserSignInPreviewDto>;
public sealed record ConfirmBrowserSignInCommand(long TelegramUserId, string Code, int Number) : IRequest;
public sealed record DeclineBrowserSignInCommand(long TelegramUserId, string Code) : IRequest;
public sealed record CheckBrowserSignInCommand(string Code, string Secret, string? CorrelationId = null) : IRequest<BrowserSignInCheck>;
public sealed record GetBrowserSessionsQuery(long TelegramUserId, string? CurrentSessionId) : IRequest<IReadOnlyList<BrowserSessionDto>>;
public sealed record EndBrowserSessionCommand(long TelegramUserId, string SessionId) : IRequest;
public sealed record EndAllBrowserSessionsCommand(long TelegramUserId, string? CorrelationId = null) : IRequest;
public sealed record SignOutBrowserCommand(string Token, string? CorrelationId = null) : IRequest;

public sealed class ConfirmBrowserSignInValidator : AbstractValidator<ConfirmBrowserSignInCommand>
{
    public ConfirmBrowserSignInValidator()
    {
        RuleFor(command => command.Code).NotEmpty().MaximumLength(64);
        RuleFor(command => command.Number).InclusiveBetween(10, 99);
    }
}

// Every browser request: the session must be live and its person still allowed (active, Business selected, an
// active member of a company). Otherwise the session is dropped at once: a removed employee, a blocked account or a
// switch to another role in Telegram ends browser access on the very next request.
public static class BrowserSessionAccess
{
    public static async Task<BrowserSessionIdentity> RequireAsync(
        IBrowserSessionStore sessions,
        IPlatformUserRepository users,
        IBusinessProfileRepository businesses,
        string token,
        CancellationToken cancellationToken)
    {
        var session = await sessions.GetSessionAsync(token, cancellationToken) ?? throw SessionEnded();
        var user = await users.GetByTelegramUserIdAsync(session.TelegramUserId, cancellationToken);
        if (user is null || (await BrowserEligibility.CheckAsync(user, businesses, cancellationToken)).Eligibility != BrowserEligibility.Ok)
        {
            await sessions.RemoveSessionAsync(token, cancellationToken);
            throw SessionEnded();
        }

        return new BrowserSessionIdentity(session, user);
    }

    public static AuthenticationException SessionEnded() => new("The browser session has ended.");
}

public sealed class StartBrowserSignInHandler(IBrowserSessionStore sessions) : IRequestHandler<StartBrowserSignInCommand, BrowserSignInStarted>
{
    public async Task<BrowserSignInStarted> Handle(StartBrowserSignInCommand command, CancellationToken cancellationToken)
    {
        var ticket = await sessions.StartSignInAsync(command.Device, cancellationToken);
        return new BrowserSignInStarted(ticket.Code, ticket.Secret, ticket.SignIn.Number, ticket.SignIn.ExpiresAtUtc);
    }
}

public sealed class PreviewBrowserSignInHandler(IBrowserSessionStore sessions, IPlatformUserRepository users, IBusinessProfileRepository businesses)
    : IRequestHandler<PreviewBrowserSignInQuery, BrowserSignInPreviewDto>
{
    public async Task<BrowserSignInPreviewDto> Handle(PreviewBrowserSignInQuery query, CancellationToken cancellationToken)
    {
        var signIn = await BrowserSignInLookup.RequirePendingAsync(sessions, query.Code, cancellationToken);
        var user = await users.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken);
        var (eligibility, membership) = user is null ? (BrowserEligibility.NoCompany, null) : await BrowserEligibility.CheckAsync(user, businesses, cancellationToken);
        return new BrowserSignInPreviewDto(signIn.Device.Browser, signIn.Device.Os, signIn.CreatedAtUtc, signIn.ExpiresAtUtc, signIn.Options, eligibility, membership?.Business.Name);
    }
}

public sealed class ConfirmBrowserSignInHandler(IBrowserSessionStore sessions, IPlatformUserRepository users, IBusinessProfileRepository businesses)
    : IRequestHandler<ConfirmBrowserSignInCommand>
{
    public async Task Handle(ConfirmBrowserSignInCommand command, CancellationToken cancellationToken)
    {
        var signIn = await BrowserSignInLookup.RequirePendingAsync(sessions, command.Code, cancellationToken);
        var user = await users.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken)
            ?? throw new BusinessRuleConflictException(BrowserSessionCodes.CompanyRequired, "Only a member of a company signs in to a browser.");
        switch ((await BrowserEligibility.CheckAsync(user, businesses, cancellationToken)).Eligibility)
        {
            case BrowserEligibility.SwitchToBusiness:
                throw new BusinessRuleConflictException(BrowserSessionCodes.BusinessRoleRequired, "Select the Business role before signing in to a browser.");
            case BrowserEligibility.NoCompany:
                throw new BusinessRuleConflictException(BrowserSessionCodes.CompanyRequired, "Only a member of a company signs in to a browser.");
        }

        // One try: a wrong number cancels the request, so guessing among the three gives one chance in three, once.
        if (command.Number != signIn.Number)
        {
            await sessions.DecideSignInAsync(command.Code, null, cancellationToken);
            throw new BusinessRuleConflictException(BrowserSessionCodes.NumberMismatch, "The number does not match the one in the browser.");
        }

        if (!await sessions.DecideSignInAsync(command.Code, user.TelegramUserId, cancellationToken))
        {
            throw BrowserSignInLookup.NotFound();
        }
    }
}

public sealed class DeclineBrowserSignInHandler(IBrowserSessionStore sessions) : IRequestHandler<DeclineBrowserSignInCommand>
{
    public async Task Handle(DeclineBrowserSignInCommand command, CancellationToken cancellationToken) =>
        await sessions.DecideSignInAsync(command.Code, null, cancellationToken);
}

// The browser asks every few seconds whether its sign-in was confirmed; the first answer after the confirmation
// opens the session (once: the request is removed before the session is created).
public sealed class CheckBrowserSignInHandler(
    IBrowserSessionStore sessions,
    IPlatformUserRepository users,
    IBusinessProfileRepository businesses,
    IAuditLogRepository auditLogs,
    IUnitOfWork unitOfWork,
    ITelegramBotClient? botClient = null,
    ILogger<CheckBrowserSignInHandler>? logger = null) : IRequestHandler<CheckBrowserSignInCommand, BrowserSignInCheck>
{
    public async Task<BrowserSignInCheck> Handle(CheckBrowserSignInCommand command, CancellationToken cancellationToken)
    {
        var signIn = await sessions.GetSignInForBrowserAsync(command.Code, command.Secret, cancellationToken);
        if (signIn is null)
        {
            return new BrowserSignInCheck(BrowserSignInStatuses.Expired, null, null);
        }

        if (signIn.Status == BrowserSignInStatuses.Pending)
        {
            return new BrowserSignInCheck(BrowserSignInStatuses.Pending, null, null);
        }

        await sessions.RemoveSignInAsync(command.Code, cancellationToken);
        if (signIn.Status != BrowserSignInStatuses.Approved || signIn.ApprovedByTelegramUserId is not { } telegramUserId)
        {
            return new BrowserSignInCheck(BrowserSignInStatuses.Declined, null, null);
        }

        // Checked again: the person may have been removed from the team or blocked since confirming.
        var user = await users.GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
        if (user is null || (await BrowserEligibility.CheckAsync(user, businesses, cancellationToken)).Eligibility != BrowserEligibility.Ok)
        {
            return new BrowserSignInCheck(BrowserSignInStatuses.Declined, null, null);
        }

        var ticket = await sessions.CreateSessionAsync(telegramUserId, signIn.Device, cancellationToken);
        try
        {
            await auditLogs.AddAsync(AuditLog.Create(telegramUserId, BrowserSessionAuditActions.SignedIn, "PlatformUser",
                telegramUserId.ToString(CultureInfo.InvariantCulture), $"{signIn.Device.Browser}/{signIn.Device.Os}", command.CorrelationId), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // No journal entry means no session: the browser never receives the cookie for it.
            await sessions.RemoveSessionAsync(ticket.Token, CancellationToken.None);
            throw;
        }

        // Every new browser is announced in the bot, with the way out.
        await BestEffortTelegramNotification.SendAsync(botClient, logger, telegramUserId, BotMessages.BrowserSignedIn(signIn.Device, ticket.Session.CreatedAtUtc), "/settings", cancellationToken);
        logger?.LogInformation("Browser signed in. Telegram user {TelegramUserId}", telegramUserId);
        return new BrowserSignInCheck(BrowserSignInStatuses.Approved, ticket, user.FirstName);
    }
}

public sealed class GetBrowserSessionsHandler(IBrowserSessionStore sessions) : IRequestHandler<GetBrowserSessionsQuery, IReadOnlyList<BrowserSessionDto>>
{
    public async Task<IReadOnlyList<BrowserSessionDto>> Handle(GetBrowserSessionsQuery query, CancellationToken cancellationToken) =>
        [.. (await sessions.ListSessionsAsync(query.TelegramUserId, cancellationToken)).Select(session => BrowserSessionDto.From(session, query.CurrentSessionId))];
}

public sealed class EndBrowserSessionHandler(IBrowserSessionStore sessions) : IRequestHandler<EndBrowserSessionCommand>
{
    public async Task Handle(EndBrowserSessionCommand command, CancellationToken cancellationToken)
    {
        if (!await sessions.RemoveSessionByIdAsync(command.TelegramUserId, command.SessionId, cancellationToken))
        {
            throw new InvalidOperationException("Browser session was not found.");
        }
    }
}

public sealed class EndAllBrowserSessionsHandler(IBrowserSessionStore sessions, IAuditLogRepository auditLogs, IUnitOfWork unitOfWork) : IRequestHandler<EndAllBrowserSessionsCommand>
{
    public async Task Handle(EndAllBrowserSessionsCommand command, CancellationToken cancellationToken)
    {
        await sessions.RemoveAllSessionsAsync(command.TelegramUserId, cancellationToken);
        await auditLogs.AddAsync(AuditLog.Create(command.TelegramUserId, BrowserSessionAuditActions.SignedOutEverywhere, "PlatformUser",
            command.TelegramUserId.ToString(CultureInfo.InvariantCulture), correlationId: command.CorrelationId), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SignOutBrowserHandler(IBrowserSessionStore sessions, IAuditLogRepository auditLogs, IUnitOfWork unitOfWork) : IRequestHandler<SignOutBrowserCommand>
{
    public async Task Handle(SignOutBrowserCommand command, CancellationToken cancellationToken)
    {
        var session = await sessions.GetSessionAsync(command.Token, cancellationToken);
        await sessions.RemoveSessionAsync(command.Token, cancellationToken);
        if (session is null)
        {
            return;
        }

        await auditLogs.AddAsync(AuditLog.Create(session.TelegramUserId, BrowserSessionAuditActions.SignedOut, "PlatformUser",
            session.TelegramUserId.ToString(CultureInfo.InvariantCulture), correlationId: command.CorrelationId), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

internal static class BrowserSignInLookup
{
    // A used, declined, expired or unknown code all read the same: the link is no longer valid.
    public static async Task<BrowserSignIn> RequirePendingAsync(IBrowserSessionStore sessions, string code, CancellationToken cancellationToken)
    {
        var signIn = await sessions.GetSignInAsync(code, cancellationToken);
        return signIn is { Status: BrowserSignInStatuses.Pending } ? signIn : throw NotFound();
    }

    public static InvalidOperationException NotFound() => new("Browser sign-in request was not found.");
}
