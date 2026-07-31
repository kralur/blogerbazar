using BloggerBazar.Application.Abstractions.Persistence;
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
    bool IsBlocked);

public sealed record GetCurrentPlatformUserCommand(long TelegramUserId, string FirstName, string? Username) : IRequest<CurrentPlatformUserDto>;

public sealed class GetCurrentPlatformUserValidator : AbstractValidator<GetCurrentPlatformUserCommand>
{
    public GetCurrentPlatformUserValidator()
    {
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(128);
        RuleFor(command => command.Username).MaximumLength(64).When(command => command.Username is not null);
    }
}

public sealed class GetCurrentPlatformUserHandler(IPlatformUserRepository users, IUnitOfWork unitOfWork)
    : IRequestHandler<GetCurrentPlatformUserCommand, CurrentPlatformUserDto>
{
    public async Task<CurrentPlatformUserDto> Handle(GetCurrentPlatformUserCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken);
        if (user is null)
        {
            user = PlatformUser.Create(command.TelegramUserId, command.FirstName.Trim(), NormalizeUsername(command.Username));
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

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(user);
    }

    internal static CurrentPlatformUserDto ToDto(PlatformUser user) => new(user.TelegramUserId, user.FirstName, user.Username, user.Role, user.SelectedMarketplaceRole, user.IsBlocked);
    private static string? NormalizeUsername(string? username) => string.IsNullOrWhiteSpace(username) ? null : username.Trim().TrimStart('@');
}
