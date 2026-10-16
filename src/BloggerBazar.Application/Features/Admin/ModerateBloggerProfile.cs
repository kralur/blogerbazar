using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Notifications;
using BloggerBazar.Domain.Entities;
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

public sealed class ModerateBloggerProfileHandler(IBloggerProfileRepository profiles, IAdminAccessPolicy adminAccess, IUnitOfWork unitOfWork, IAuditLogRepository auditLogs, ITelegramBotClient? botClient = null, ILogger<ModerateBloggerProfileHandler>? logger = null)
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

        // Every moderation decision is in the journal, like campaign moderation and blocking.
        await auditLogs.AddAsync(AuditLog.Create(command.TelegramUserId, AdminModerationAudit.Action("blogger-profile", command.Approve, command.NeedsChanges), "BloggerProfile", profile.Id.ToString()), cancellationToken);
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

// Journal actions of profile moderation: "<target>.approved", "<target>.rejected" or "<target>.needs-changes".
public static class AdminModerationAudit
{
    public static string Action(string target, bool approve, bool needsChanges) =>
        $"{target}.{(needsChanges ? "needs-changes" : approve ? "approved" : "rejected")}";
}
