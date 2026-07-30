using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Users;

public sealed record SelectMarketplaceRoleCommand(long TelegramUserId, MarketplaceRole Role) : IRequest<CurrentPlatformUserDto>;

public sealed class SelectMarketplaceRoleValidator : AbstractValidator<SelectMarketplaceRoleCommand>
{
    public SelectMarketplaceRoleValidator()
    {
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.Role).IsInEnum();
    }
}

public sealed class SelectMarketplaceRoleHandler(IPlatformUserRepository users, IUnitOfWork unitOfWork)
    : IRequestHandler<SelectMarketplaceRoleCommand, CurrentPlatformUserDto>
{
    public async Task<CurrentPlatformUserDto> Handle(SelectMarketplaceRoleCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken)
            ?? throw new InvalidOperationException("Open the application through Telegram before selecting a role.");
        user.SelectMarketplaceRole(command.Role);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return GetCurrentPlatformUserHandler.ToDto(user);
    }
}
