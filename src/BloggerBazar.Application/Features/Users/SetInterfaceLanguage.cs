using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Users;

// The Mini App reports the interface language so bot messages can use it (D39).
public sealed record SetInterfaceLanguageCommand(long TelegramUserId, string Language) : IRequest<CurrentPlatformUserDto>;

public sealed class SetInterfaceLanguageValidator : AbstractValidator<SetInterfaceLanguageCommand>
{
    public SetInterfaceLanguageValidator()
    {
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.Language).Must(InterfaceLanguage.IsSupported).WithMessage("The interface language is not supported.");
    }
}

public sealed class SetInterfaceLanguageHandler(IPlatformUserRepository users, IUnitOfWork unitOfWork)
    : IRequestHandler<SetInterfaceLanguageCommand, CurrentPlatformUserDto>
{
    public async Task<CurrentPlatformUserDto> Handle(SetInterfaceLanguageCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken);
        if (user is null || user.IsDeleted)
        {
            throw new InvalidOperationException("Platform user was not found.");
        }

        if (user.PreferredLanguage != command.Language)
        {
            user.SetPreferredLanguage(command.Language);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return GetCurrentPlatformUserHandler.ToDto(user);
    }
}
