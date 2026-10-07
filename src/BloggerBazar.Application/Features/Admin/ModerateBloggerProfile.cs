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
            ? BotMessages.BloggerProfileNeedsChanges
            : command.Approve
                ? BotMessages.BloggerProfileApproved
                : BotMessages.BloggerProfileRejected;
        await BestEffortTelegramNotification.SendAsync(botClient, logger, profile.TelegramUserId, notification, cancellationToken);
        return AdminBloggerProfileDto.From(profile);
    }
}
