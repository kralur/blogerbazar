using System.Globalization;
using System.Security.Authentication;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.AdminWeb;

// Web admin sign-in (Telegram Login Widget) and its server-side session. Access is the same as for the Mini App
// admin: IAdminAccessPolicy (Owner or Admin, not blocked, not deleted), checked again on every request.

// Role as a D26 string; the web admin translates it.
public static class AdminWebRoles
{
    public const string Owner = "owner";
    public const string Admin = "admin";
}

public static class AdminWebAuditActions
{
    public const string SignedIn = "admin-web.signed-in";
    public const string SignedOut = "admin-web.signed-out";
    public const string SignInDenied = "admin-web.sign-in-denied";
    public const string SignedOutEverywhere = "admin-web.signed-out-everywhere";
    public const string Exported = "admin-web.exported";
}

public sealed record AdminWebSessionDto(long TelegramUserId, string FirstName, string? Username, string Role, DateTime ExpiresAtUtc)
{
    public static AdminWebSessionDto From(PlatformUser user, AdminWebSession session) => new(
        user.TelegramUserId,
        user.FirstName,
        user.Username,
        user.Role == PlatformRole.Owner ? AdminWebRoles.Owner : AdminWebRoles.Admin,
        session.ExpiresAtUtc);
}

public sealed record AdminWebSignInResult(string Token, AdminWebSessionDto Session);

// Fields are the widget data exactly as Telegram returned it (id, first_name, username, photo_url, auth_date, hash, ...).
public sealed record StartAdminWebSessionCommand(IReadOnlyDictionary<string, string> Fields, string? CorrelationId = null) : IRequest<AdminWebSignInResult>;
public sealed record GetAdminWebSessionQuery(string Token) : IRequest<AdminWebSessionDto>;
public sealed record EndAdminWebSessionCommand(string Token, string? CorrelationId = null) : IRequest;
public sealed record EndAllAdminWebSessionsCommand(string Token, string? CorrelationId = null) : IRequest;

// CSV exports are built in the browser from what the admin sees; the journal entry is written first, so a file with
// people's data never leaves without a trace. Kind is one of AdminWebExports.Kinds (D26 strings).
public sealed record RecordAdminWebExportCommand(string Token, string Kind, int Rows, string? CorrelationId = null) : IRequest;

public static class AdminWebExports
{
    public const int MaxRows = 100_000;
    // Lists with people's names, usernames, phones or Telegram ids.
    public static readonly IReadOnlySet<string> Personal = new HashSet<string>(StringComparer.Ordinal) { "users", "deals", "complaints", "journal" };
    public static readonly IReadOnlySet<string> Kinds = new HashSet<string>(Personal.Concat(["daily", "money", "cities", "categories", "sources", "funnel"]), StringComparer.Ordinal);
}

public sealed class RecordAdminWebExportValidator : AbstractValidator<RecordAdminWebExportCommand>
{
    public RecordAdminWebExportValidator()
    {
        RuleFor(command => command.Kind).Must(kind => kind is not null && AdminWebExports.Kinds.Contains(kind));
        RuleFor(command => command.Rows).InclusiveBetween(0, AdminWebExports.MaxRows);
    }
}

public sealed class StartAdminWebSessionValidator : AbstractValidator<StartAdminWebSessionCommand>
{
    private const int MaxFields = 16;
    private const int MaxValueLength = 512;

    public StartAdminWebSessionValidator()
    {
        RuleFor(command => command.Fields)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(fields => fields.Count is > 0 and <= MaxFields)
            .Must(fields => fields.All(pair => IsFieldName(pair.Key) && pair.Value is { Length: <= MaxValueLength }));
    }

    private static bool IsFieldName(string name) =>
        name.Length is > 0 and <= 32 && name.All(character => character is >= 'a' and <= 'z' or '_');
}

// Every web admin request: the session must exist and its owner must still be an active Owner or Admin. Otherwise the
// session is dropped at once, so a blocked or demoted admin loses web access on the very next request.
public static class AdminWebSessionAccess
{
    public static async Task<AdminWebSession> RequireAsync(IAdminWebSessionStore sessions, IAdminAccessPolicy access, string token, CancellationToken cancellationToken)
    {
        var session = await sessions.GetAsync(token, cancellationToken)
            ?? throw new AuthenticationException("The web admin session has ended.");
        try
        {
            access.EnsureAllowed(session.TelegramUserId);
        }
        catch (UnauthorizedAccessException)
        {
            await sessions.RemoveAsync(token, cancellationToken);
            throw;
        }

        return session;
    }
}

public sealed class StartAdminWebSessionHandler(
    ITelegramLoginWidgetValidator validator,
    IAdminWebSessionStore sessions,
    IAdminAccessPolicy access,
    IPlatformUserRepository users,
    IAuditLogRepository auditLogs,
    IUnitOfWork unitOfWork,
    ILogger<StartAdminWebSessionHandler>? logger = null) : IRequestHandler<StartAdminWebSessionCommand, AdminWebSignInResult>
{
    public async Task<AdminWebSignInResult> Handle(StartAdminWebSessionCommand command, CancellationToken cancellationToken)
    {
        var telegramUser = validator.Validate(command.Fields);
        if (!await sessions.TryUseSignInAsync(telegramUser.Hash, cancellationToken))
        {
            throw new AuthenticationException("This Telegram sign-in was already used.");
        }

        var telegramUserId = telegramUser.Id.ToString(CultureInfo.InvariantCulture);
        try
        {
            access.EnsureAllowed(telegramUser.Id);
        }
        catch (UnauthorizedAccessException)
        {
            // The Telegram account is genuine (signature checked) but not an active Owner/Admin: keep a trace.
            logger?.LogWarning("Web admin sign-in denied for Telegram user {TelegramUserId}", telegramUser.Id);
            await auditLogs.AddAsync(AuditLog.Create(telegramUser.Id, AdminWebAuditActions.SignInDenied, "PlatformUser", telegramUserId, correlationId: command.CorrelationId), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }

        var user = await users.GetByTelegramUserIdAsync(telegramUser.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("Administrator access is required.");
        var ticket = await sessions.CreateAsync(user.TelegramUserId, cancellationToken);
        try
        {
            await auditLogs.AddAsync(AuditLog.Create(user.TelegramUserId, AdminWebAuditActions.SignedIn, "PlatformUser", telegramUserId, correlationId: command.CorrelationId), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // No journal entry means no session: the browser never receives the cookie for it anyway.
            await sessions.RemoveAsync(ticket.Token, CancellationToken.None);
            throw;
        }

        logger?.LogInformation("Web admin signed in. Telegram user {TelegramUserId}", user.TelegramUserId);
        return new AdminWebSignInResult(ticket.Token, AdminWebSessionDto.From(user, ticket.Session));
    }
}

public sealed class GetAdminWebSessionHandler(IAdminWebSessionStore sessions, IAdminAccessPolicy access, IPlatformUserRepository users)
    : IRequestHandler<GetAdminWebSessionQuery, AdminWebSessionDto>
{
    public async Task<AdminWebSessionDto> Handle(GetAdminWebSessionQuery query, CancellationToken cancellationToken)
    {
        var session = await AdminWebSessionAccess.RequireAsync(sessions, access, query.Token, cancellationToken);
        var user = await users.GetByTelegramUserIdAsync(session.TelegramUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Administrator access is required.");
        return AdminWebSessionDto.From(user, session);
    }
}

public sealed class EndAdminWebSessionHandler(IAdminWebSessionStore sessions, IAuditLogRepository auditLogs, IUnitOfWork unitOfWork)
    : IRequestHandler<EndAdminWebSessionCommand>
{
    public async Task Handle(EndAdminWebSessionCommand command, CancellationToken cancellationToken)
    {
        var session = await sessions.GetAsync(command.Token, cancellationToken);
        await sessions.RemoveAsync(command.Token, cancellationToken);
        if (session is null)
        {
            return;
        }

        await auditLogs.AddAsync(AuditLog.Create(session.TelegramUserId, AdminWebAuditActions.SignedOut, "PlatformUser", session.TelegramUserId.ToString(CultureInfo.InvariantCulture), correlationId: command.CorrelationId), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

// "Sign out on all devices": only from a live session of an active Owner/Admin, for that person's own sessions.
public sealed class EndAllAdminWebSessionsHandler(IAdminWebSessionStore sessions, IAdminAccessPolicy access, IAuditLogRepository auditLogs, IUnitOfWork unitOfWork)
    : IRequestHandler<EndAllAdminWebSessionsCommand>
{
    public async Task Handle(EndAllAdminWebSessionsCommand command, CancellationToken cancellationToken)
    {
        var session = await AdminWebSessionAccess.RequireAsync(sessions, access, command.Token, cancellationToken);
        await sessions.RemoveAllAsync(session.TelegramUserId, cancellationToken);
        await sessions.RemoveAsync(command.Token, cancellationToken);
        await auditLogs.AddAsync(AuditLog.Create(session.TelegramUserId, AdminWebAuditActions.SignedOutEverywhere, "PlatformUser", session.TelegramUserId.ToString(CultureInfo.InvariantCulture), correlationId: command.CorrelationId), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class RecordAdminWebExportHandler(IAdminWebSessionStore sessions, IAdminAccessPolicy access, IAuditLogRepository auditLogs, IUnitOfWork unitOfWork)
    : IRequestHandler<RecordAdminWebExportCommand>
{
    public async Task Handle(RecordAdminWebExportCommand command, CancellationToken cancellationToken)
    {
        var session = await AdminWebSessionAccess.RequireAsync(sessions, access, command.Token, cancellationToken);
        // Target id is the kind, details the number of rows; the site names both.
        await auditLogs.AddAsync(AuditLog.Create(session.TelegramUserId, AdminWebAuditActions.Exported, "Export", command.Kind, command.Rows.ToString(CultureInfo.InvariantCulture), command.CorrelationId), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
