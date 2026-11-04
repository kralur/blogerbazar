using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Users;

public sealed record CurrentPlatformUserDto(
    long TelegramUserId,
    string FirstName,
    string? Username,
    PlatformRole Role,
    MarketplaceRole? SelectedMarketplaceRole,
    bool IsBlocked,
    string? PreferredLanguage = null,
    string? VerifiedPhone = null,
    bool TipsEnabled = true);

// Source: the ad label of the link that opened the app (bb_src); kept only for an account seen for the first time.
public sealed record GetCurrentPlatformUserCommand(long TelegramUserId, string FirstName, string? Username, string? Source = null) : IRequest<CurrentPlatformUserDto>;

public sealed class GetCurrentPlatformUserValidator : AbstractValidator<GetCurrentPlatformUserCommand>
{
    public GetCurrentPlatformUserValidator()
    {
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(128);
        RuleFor(command => command.Username).MaximumLength(64).When(command => command.Username is not null);
    }
}

public sealed class GetCurrentPlatformUserHandler(IPlatformUserRepository users, IUnitOfWork unitOfWork, IConfiguredOwners? owners = null, IAuditLogRepository? auditLogs = null)
    : IRequestHandler<GetCurrentPlatformUserCommand, CurrentPlatformUserDto>
{
    // Pre-launch audit 2026-11-04: a new person's app opens with two GET /me at once; both try to create the account and
    // the unique Telegram id lets only one in. The other now reads the account its twin created instead of a 500.
    public async Task<CurrentPlatformUserDto> Handle(GetCurrentPlatformUserCommand command, CancellationToken cancellationToken)
    {
        var first = await TryHandleAsync(command, cancellationToken);
        return first ?? await TryHandleAsync(command, cancellationToken)
            ?? throw new InvalidOperationException("The account was changed by another request at the same time.");
    }

    private async Task<CurrentPlatformUserDto?> TryHandleAsync(GetCurrentPlatformUserCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken);
        if (user is null)
        {
            user = PlatformUser.Create(command.TelegramUserId, command.FirstName.Trim(), NormalizeUsername(command.Username));
            user.SetSourceOnce(command.Source?.Trim().ToLowerInvariant());
            await users.AddAsync(user, cancellationToken);
        }
        else
        {
            if (user.IsBlocked)
            {
                throw new UnauthorizedAccessException("This Telegram account is not allowed to access the marketplace.");
            }

            if (user.IsDeleted)
            {
                user.RestoreForNewOnboarding(command.FirstName.Trim(), NormalizeUsername(command.Username));
            }
            else
            {
                user.SyncTelegramIdentity(command.FirstName.Trim(), NormalizeUsername(command.Username));
            }
        }

        // The owner named in the deployment settings gets the Owner role on sign-in; nobody can grant it in the app.
        if (owners?.IsConfiguredOwner(user.TelegramUserId) == true && user.Role != PlatformRole.Owner)
        {
            user.SetRole(PlatformRole.Owner);
        }
        // Audit 2026-10-22: and loses it once the id is taken out of the settings (a former co-owner), with a journal entry.
        else if (owners is not null && user.Role == PlatformRole.Owner && !owners.IsConfiguredOwner(user.TelegramUserId))
        {
            user.SetRole(PlatformRole.Member);
            if (auditLogs is not null)
            {
                await auditLogs.AddAsync(AuditLog.Create(user.TelegramUserId, "platform-user.owner-revoked", "PlatformUser", user.TelegramUserId.ToString()), cancellationToken);
            }
        }

        user.MarkSeen(DateTime.UtcNow);
        return await unitOfWork.TrySaveChangesAsync(cancellationToken) ? ToDto(user) : null;
    }

    internal static CurrentPlatformUserDto ToDto(PlatformUser user) => new(user.TelegramUserId, user.FirstName, user.Username, user.Role, user.SelectedMarketplaceRole, user.IsBlocked, user.PreferredLanguage, user.VerifiedPhone, !user.TipsDisabled);
    private static string? NormalizeUsername(string? username) => string.IsNullOrWhiteSpace(username) ? null : username.Trim().TrimStart('@');
}
