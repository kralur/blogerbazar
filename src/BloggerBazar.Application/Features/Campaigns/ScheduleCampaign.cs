using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Notifications;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.Campaigns;

// D69: a business plans when a draft or closed campaign goes to the catalog; a background job publishes it then.
public sealed record ScheduleCampaignCommand(Guid CampaignId, long TelegramUserId, DateTime PublishAtUtc) : IRequest<CampaignDto>;
public sealed record CancelCampaignScheduleCommand(Guid CampaignId, long TelegramUserId) : IRequest<CampaignDto>;
public sealed record PublishScheduledCampaignsCommand(DateTime NowUtc) : IRequest<int>;

internal static class CampaignSchedule
{
    // At least a minute ahead (the job runs every minute) and at most two months ahead.
    internal static readonly TimeSpan MinimumLead = TimeSpan.FromMinutes(1);
    internal static readonly TimeSpan MaximumLead = TimeSpan.FromDays(60);

    internal static bool IsAllowed(DateTime publishAtUtc, DateTime nowUtc)
    {
        var at = publishAtUtc.ToUniversalTime();
        return at >= nowUtc + MinimumLead && at <= nowUtc + MaximumLead;
    }

    // Only a draft or a closed campaign waits for a planned publication; a rejected one is not the owner's to publish.
    internal static void EnsureCanSchedule(Campaign campaign, DateTime publishAtUtc)
    {
        if (campaign.Status == CampaignStatus.Rejected) throw new InvalidOperationException("A rejected campaign cannot be published.");
        if (campaign.Status == CampaignStatus.Published) throw new BusinessRuleConflictException("campaign_already_published", "The campaign is already published.");
        // A campaign whose deadline has passed by the planned moment would go out already expired.
        if (campaign.IsExpired(publishAtUtc)) throw new BusinessRuleConflictException("campaign_expired", "The campaign deadline has passed by the planned time.");
    }
}

public sealed class ScheduleCampaignValidator : AbstractValidator<ScheduleCampaignCommand>
{
    public ScheduleCampaignValidator()
    {
        RuleFor(command => command.CampaignId).NotEmpty();
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.PublishAtUtc).Must(at => CampaignSchedule.IsAllowed(at, DateTime.UtcNow))
            .WithMessage("The publication time must be between a minute and 60 days from now.");
    }
}

public sealed class CancelCampaignScheduleValidator : AbstractValidator<CancelCampaignScheduleCommand>
{
    public CancelCampaignScheduleValidator()
    {
        RuleFor(command => command.CampaignId).NotEmpty();
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
    }
}

public sealed class ScheduleCampaignHandler(ICampaignRepository campaigns, IPlatformUserRepository users, IBusinessProfileRepository businesses, IUnitOfWork unitOfWork)
    : IRequestHandler<ScheduleCampaignCommand, CampaignDto>
{
    public async Task<CampaignDto> Handle(ScheduleCampaignCommand command, CancellationToken cancellationToken)
    {
        var business = await CampaignManagementAccess.RequireBusinessAsync(users, businesses, command.TelegramUserId, cancellationToken);
        var campaign = await campaigns.GetByIdForBusinessAsync(command.CampaignId, business.Id, cancellationToken) ?? throw new InvalidOperationException("Campaign was not found.");
        var publishAtUtc = command.PublishAtUtc.ToUniversalTime();
        CampaignSchedule.EnsureCanSchedule(campaign, publishAtUtc);
        campaign.SchedulePublication(publishAtUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CampaignDto.From(campaign, business.Name);
    }
}

public sealed class CancelCampaignScheduleHandler(ICampaignRepository campaigns, IPlatformUserRepository users, IBusinessProfileRepository businesses, IUnitOfWork unitOfWork)
    : IRequestHandler<CancelCampaignScheduleCommand, CampaignDto>
{
    public async Task<CampaignDto> Handle(CancelCampaignScheduleCommand command, CancellationToken cancellationToken)
    {
        var business = await CampaignManagementAccess.RequireBusinessAsync(users, businesses, command.TelegramUserId, cancellationToken);
        var campaign = await campaigns.GetByIdForBusinessAsync(command.CampaignId, business.Id, cancellationToken) ?? throw new InvalidOperationException("Campaign was not found.");
        if (campaign.ScheduledPublishAtUtc is not null)
        {
            campaign.CancelScheduledPublication();
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return CampaignDto.From(campaign, business.Name);
    }
}

// Runs every minute. Safe on several instances: each publication is a conditional update, so only one of them sends the message.
public sealed class PublishScheduledCampaignsHandler(
    ICampaignRepository campaigns,
    ICatalogCache? cache = null,
    ITelegramBotClient? botClient = null,
    ILogger<PublishScheduledCampaignsHandler>? logger = null) : IRequestHandler<PublishScheduledCampaignsCommand, int>
{
    internal const int BatchSize = 50;

    public async Task<int> Handle(PublishScheduledCampaignsCommand command, CancellationToken cancellationToken)
    {
        var published = 0;
        foreach (var due in await campaigns.ListDueScheduledAsync(command.NowUtc, BatchSize, cancellationToken))
        {
            var route = $"/my-campaign/{due.Id}";
            if (due.Deadline.HasValue && due.Deadline.Value.Date < command.NowUtc.Date)
            {
                if (await campaigns.TryDropScheduleAsync(due.Id, due.ScheduledPublishAtUtc, command.NowUtc, cancellationToken))
                    await BestEffortTelegramNotification.SendAsync(botClient, logger, due.OwnerTelegramUserId, BotMessages.ScheduledCampaignNotPublished(due.Title), route, cancellationToken);
                continue;
            }

            if (await campaigns.TryPublishScheduledAsync(due.Id, due.ScheduledPublishAtUtc, command.NowUtc, cancellationToken))
            {
                published++;
                await BestEffortTelegramNotification.SendAsync(botClient, logger, due.OwnerTelegramUserId, BotMessages.ScheduledCampaignPublished(due.Title), route, cancellationToken);
            }
        }

        if (published > 0 && cache is not null) await CampaignCatalogCache.InvalidateAsync(cache, cancellationToken);
        return published;
    }
}
