using BloggerBazar.Application.Abstractions.Persistence;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Users;

// D54: the user switches the bot tips on or off in Settings.
public sealed record SetBotTipsCommand(long TelegramUserId, bool Enabled) : IRequest<CurrentPlatformUserDto>;

public sealed class SetBotTipsValidator : AbstractValidator<SetBotTipsCommand>
{
    public SetBotTipsValidator() => RuleFor(command => command.TelegramUserId).GreaterThan(0);
}

public sealed class SetBotTipsHandler(IPlatformUserRepository users, IUnitOfWork unitOfWork)
    : IRequestHandler<SetBotTipsCommand, CurrentPlatformUserDto>
{
    public async Task<CurrentPlatformUserDto> Handle(SetBotTipsCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken);
        if (user is null || user.IsDeleted)
        {
            throw new InvalidOperationException("Platform user was not found.");
        }

        if (user.TipsDisabled == command.Enabled)
        {
            user.SetTipsDisabled(!command.Enabled);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return GetCurrentPlatformUserHandler.ToDto(user);
    }
}
