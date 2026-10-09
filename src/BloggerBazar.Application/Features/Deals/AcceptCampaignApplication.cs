using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Notifications;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.Deals;

public sealed record AcceptCampaignApplicationCommand(Guid CampaignApplicationId, long TelegramUserId) : IRequest<DealDto>;

public sealed class AcceptCampaignApplicationValidator : AbstractValidator<AcceptCampaignApplicationCommand>
{
    public AcceptCampaignApplicationValidator()
    {
        RuleFor(command => command.CampaignApplicationId).NotEmpty();
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
    }
}

public sealed class AcceptCampaignApplicationHandler(
    ICampaignApplicationRepository applications,
    IPlatformUserRepository users,
    IBusinessProfileRepository businesses,
    IDealRepository deals,
    IUnitOfWork unitOfWork,
    IBloggerProfileRepository? bloggers = null,
    ITelegramBotClient? botClient = null,
    ILogger<AcceptCampaignApplicationHandler>? logger = null,
    IBrandFaceProfileRepository? brandFaces = null) : IRequestHandler<AcceptCampaignApplicationCommand, DealDto>
{
    public async Task<DealDto> Handle(AcceptCampaignApplicationCommand command, CancellationToken cancellationToken)
    {
        var application = await applications.GetByIdAsync(command.CampaignApplicationId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign application was not found.");
        var business = await DealAccess.RequireBusinessAsync(users, businesses, command.TelegramUserId, cancellationToken);
        if (application.Campaign.BusinessId != business.Id)
        {
            throw new UnauthorizedAccessException("You cannot accept an application for another business.");
        }

        var existingDeal = await deals.GetByCampaignApplicationIdAsync(application.Id, cancellationToken);
        if (application.Status == CampaignApplicationStatus.Accepted)
        {
            return existingDeal is not null
                ? DealDto.From(existingDeal)
                : throw new InvalidOperationException("Accepted campaign application has no deal.");
        }

        if (application.Status is not (CampaignApplicationStatus.Sent or CampaignApplicationStatus.Viewed))
        {
            throw new InvalidOperationException("Only pending applications can be accepted.");
        }

        if (existingDeal is not null)
        {
            throw new InvalidOperationException("A deal already exists for this application.");
        }

        application.Accept();
        var deal = Deal.Create(application.Id, application.CreatorRole, application.CreatorId, business.Id, CampaignTermsSnapshot.FromCampaign(application.Campaign));
        await deals.AddAsync(deal, cancellationToken);
        if (!await unitOfWork.TrySaveChangesAsync(cancellationToken))
        {
            var persistedDeal = await deals.GetByCampaignApplicationIdAsync(application.Id, cancellationToken);
            if (persistedDeal is not null)
            {
                return DealDto.From(persistedDeal);
            }

            throw new InvalidOperationException("Campaign application decision conflicts with an existing deal.");
        }
        var creatorChatId = bloggers is null ? null : await Campaigns.CampaignApplicationAccess.FindCreatorTelegramUserIdAsync(bloggers, brandFaces, application, cancellationToken);
        if (creatorChatId is not null) await BestEffortTelegramNotification.SendAsync(botClient, logger, creatorChatId.Value, BotMessages.CampaignApplicationAccepted(application.Campaign.Title), $"/deal/{deal.Id}", cancellationToken);
        return DealDto.From(deal);
    }
}
