using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Notifications;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.Campaigns;

public sealed record ApplyToCampaignCommand(Guid CampaignId, long TelegramUserId, string? Message) : IRequest<CampaignApplicationDto>;

public sealed record CampaignApplicationDto(Guid Id, Guid CampaignId, Guid? BloggerId, string? Message, int Status, DateTime CreatedAtUtc, Guid? BrandFaceId = null)
{
    public static CampaignApplicationDto From(CampaignApplication application) => new(application.Id, application.CampaignId, application.BloggerId, application.Message, (int)application.Status, application.CreatedAtUtc, application.BrandFaceId);
}

public sealed class ApplyToCampaignValidator : AbstractValidator<ApplyToCampaignCommand>
{
    public ApplyToCampaignValidator()
    {
        RuleFor(command => command.CampaignId).NotEmpty();
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.Message).MaximumLength(1000).When(command => command.Message is not null);
    }
}

public sealed class ApplyToCampaignHandler(
    ICampaignRepository campaigns,
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    ICampaignApplicationRepository applications,
    IUnitOfWork unitOfWork,
    ICatalogCache? cache = null,
    ITelegramBotClient? botClient = null,
    ILogger<ApplyToCampaignHandler>? logger = null,
    IBrandFaceProfileRepository? brandFaces = null) : IRequestHandler<ApplyToCampaignCommand, CampaignApplicationDto>
{
    public async Task<CampaignApplicationDto> Handle(ApplyToCampaignCommand command, CancellationToken cancellationToken)
    {
        var creator = await CampaignApplicationAccess.RequireCreatorAsync(users, bloggers, brandFaces, command.TelegramUserId, cancellationToken);
        var campaign = await campaigns.GetByIdAsync(command.CampaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign was not found.");
        var owner = await users.GetByTelegramUserIdAsync(campaign.Business.TelegramUserId, cancellationToken);
        if (campaign.Status != CampaignStatus.Published
            || campaign.Business.IsDeleted
            || campaign.Business.ModerationStatus != BloggerStatus.Approved
            || owner is null
            || owner.IsBlocked
            || owner.IsDeleted)
        {
            throw new InvalidOperationException("Campaign was not found.");
        }

        if (campaign.IsExpired(DateTime.UtcNow))
        {
            throw new BusinessRuleConflictException("campaign_expired", "The campaign deadline has passed.");
        }

        var business = await businesses.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken);
        if (business?.Id == campaign.BusinessId)
        {
            throw new InvalidOperationException("You cannot apply to your own campaign.");
        }

        if (await applications.ExistsAsync(campaign.Id, creator.Role, creator.ProfileId, cancellationToken))
        {
            throw new InvalidOperationException("You have already applied to this campaign.");
        }

        var application = creator.Role == MarketplaceRole.BrandFace
            ? CampaignApplication.CreateForBrandFace(campaign.Id, creator.ProfileId, command.Message?.Trim())
            : CampaignApplication.Create(campaign.Id, creator.ProfileId, command.Message?.Trim());
        await applications.AddAsync(application, cancellationToken);
        if (!await unitOfWork.TrySaveChangesAsync(cancellationToken))
        {
            throw new InvalidOperationException("You have already applied to this campaign.");
        }
        if (cache is not null)
        {
            await CampaignCatalogCache.InvalidateAsync(cache, cancellationToken);
        }
        if (campaign.Business is not null)
        {
            await BestEffortTelegramNotification.SendAsync(botClient, logger, campaign.Business.TelegramUserId, BotMessages.NewCampaignApplication(creator.Name, campaign.Title), $"/my-campaign-applications/{campaign.Id}", cancellationToken);
        }
        return CampaignApplicationDto.From(application);
    }
}
