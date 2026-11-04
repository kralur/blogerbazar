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

public sealed class GetCurrentPlatformUserHandler(IPlatformUserRepository users, IUnitOfWork unitOfWork, IConfiguredOwners? owners = null)
    : IRequestHandler<GetCurrentPlatformUserCommand, CurrentPlatformUserDto>
{
    public async Task<CurrentPlatformUserDto> Handle(GetCurrentPlatformUserCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken);
        if (user is null)
        {
            var created = PlatformUser.Create(command.TelegramUserId, command.FirstName.Trim(), NormalizeUsername(command.Username));
            created.SetSourceOnce(command.Source?.Trim().ToLowerInvariant());
            await users.AddAsync(created, cancellationToken);
            PromoteConfiguredOwner(created);
            created.MarkSeen(DateTime.UtcNow);
            // Two first requests of one account in the same instant (the app's start and the bot, or parallel screens):
            // the unique index lets one through; the other reads the account that won and continues as a returning one.
            if (await unitOfWork.TrySaveChangesAsync(cancellationToken))
            {
                return ToDto(created);
            }

            user = await users.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken)
                ?? throw new InvalidOperationException("The account was created by a parallel request but could not be read.");
        }

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

        PromoteConfiguredOwner(user);
        user.MarkSeen(DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(user);
    }

    // The owner named in the deployment settings gets the Owner role on sign-in; nobody can grant it in the app.
    private void PromoteConfiguredOwner(PlatformUser user)
    {
        if (owners?.IsConfiguredOwner(user.TelegramUserId) == true && user.Role != PlatformRole.Owner)
        {
            user.SetRole(PlatformRole.Owner);
        }
    }

    internal static CurrentPlatformUserDto ToDto(PlatformUser user) => new(user.TelegramUserId, user.FirstName, user.Username, user.Role, user.SelectedMarketplaceRole, user.IsBlocked, user.PreferredLanguage, user.VerifiedPhone, !user.TipsDisabled);
    private static string? NormalizeUsername(string? username) => string.IsNullOrWhiteSpace(username) ? null : username.Trim().TrimStart('@');
}
