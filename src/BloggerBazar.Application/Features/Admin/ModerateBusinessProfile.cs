using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Notifications;
using BloggerBazar.Application.Features.Businesses;
using BloggerBazar.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.Admin;

public sealed record ModerateBusinessProfileCommand(Guid BusinessId, long TelegramUserId, bool Approve, bool NeedsChanges = false) : IRequest<BusinessProfileDto>;

public sealed record GetPendingBusinessProfilesQuery(long TelegramUserId, int Take = 50) : IRequest<IReadOnlyList<BusinessProfileDto>>;

public sealed class ModerateBusinessProfileHandler(IBusinessProfileRepository businesses, IAdminAccessPolicy access, IUnitOfWork unitOfWork, ITelegramBotClient? botClient = null, ILogger<ModerateBusinessProfileHandler>? logger = null) : IRequestHandler<ModerateBusinessProfileCommand, BusinessProfileDto>
{
    public async Task<BusinessProfileDto> Handle(ModerateBusinessProfileCommand command, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(command.TelegramUserId);
        var profile = await businesses.GetByIdAsync(command.BusinessId, cancellationToken) ?? throw new InvalidOperationException("Business profile was not found.");
        if (profile.ModerationStatus != BloggerStatus.Pending)
        {
            throw new InvalidOperationException("Only pending business profiles can be moderated.");
        }
        if (command.NeedsChanges) profile.RequestChanges(); else if (command.Approve) profile.Approve(); else profile.Reject();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await BestEffortTelegramNotification.SendAsync(botClient, logger, profile.TelegramUserId, command.Approve ? "🎉 Профиль компании одобрен и доступен в BloggerBazar." : command.NeedsChanges ? "BloggerBazar: необходимо исправить несколько пунктов профиля компании." : "BloggerBazar: профиль компании не прошёл проверку.", cancellationToken);
        return BusinessProfileDto.From(profile);
    }
}

public sealed class GetPendingBusinessProfilesHandler(IBusinessProfileRepository businesses, IAdminAccessPolicy access) : IRequestHandler<GetPendingBusinessProfilesQuery, IReadOnlyList<BusinessProfileDto>>
{
    public async Task<IReadOnlyList<BusinessProfileDto>> Handle(GetPendingBusinessProfilesQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.TelegramUserId);
        return (await businesses.GetPendingAsync(Math.Clamp(query.Take, 1, 100), cancellationToken)).Select(BusinessProfileDto.From).ToArray();
    }
}
