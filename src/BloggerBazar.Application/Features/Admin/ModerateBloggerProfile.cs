using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Notifications;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.Admin;

public sealed record ModerateBloggerProfileCommand(Guid BloggerId, long TelegramUserId, bool Approve, bool NeedsChanges = false) : IRequest<AdminBloggerProfileDto>;

public sealed class ModerateBloggerProfileValidator : AbstractValidator<ModerateBloggerProfileCommand>
{
    public ModerateBloggerProfileValidator()
    {
        RuleFor(command => command.BloggerId).NotEmpty();
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
    }
}

public sealed class ModerateBloggerProfileHandler(IBloggerProfileRepository profiles, IAdminAccessPolicy adminAccess, IUnitOfWork unitOfWork, ITelegramBotClient? botClient = null, ILogger<ModerateBloggerProfileHandler>? logger = null)
    : IRequestHandler<ModerateBloggerProfileCommand, AdminBloggerProfileDto>
{
    public async Task<AdminBloggerProfileDto> Handle(ModerateBloggerProfileCommand command, CancellationToken cancellationToken)
    {
        adminAccess.EnsureAllowed(command.TelegramUserId);
        var profile = await profiles.GetByIdAsync(command.BloggerId, cancellationToken)
            ?? throw new InvalidOperationException("Blogger profile was not found.");
        if (profile.Status != BloggerStatus.Pending)
        {
            throw new InvalidOperationException("Only pending blogger profiles can be moderated.");
        }

        if (command.NeedsChanges)
        {
            profile.RequestChanges();
        }
        else if (command.Approve)
        {
            profile.Approve();
        }
        else
        {
            profile.Reject();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        var notification = command.NeedsChanges
            ? "BloggerBazar: необходимо исправить несколько пунктов. После сохранения отправьте профиль ещё раз."
            : command.Approve
                ? "🎉 Поздравляем! Ваш профиль успешно прошёл модерацию. Теперь он доступен пользователям BloggerBazar."
                : "BloggerBazar: анкета не прошла проверку. Исправьте замечания и отправьте снова.";
        await BestEffortTelegramNotification.SendAsync(botClient, logger, profile.TelegramUserId, notification, cancellationToken);
        return AdminBloggerProfileDto.From(profile);
    }
}
