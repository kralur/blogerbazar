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
    IBusinessProfileRepository businesses,
    IDealRepository deals,
    IUnitOfWork unitOfWork,
    IBloggerProfileRepository? bloggers = null,
    ITelegramBotClient? botClient = null,
    ILogger<AcceptCampaignApplicationHandler>? logger = null) : IRequestHandler<AcceptCampaignApplicationCommand, DealDto>
{
    public async Task<DealDto> Handle(AcceptCampaignApplicationCommand command, CancellationToken cancellationToken)
    {
        var application = await applications.GetByIdAsync(command.CampaignApplicationId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign application was not found.");
        if (application.Status is not (CampaignApplicationStatus.Sent or CampaignApplicationStatus.Viewed))
        {
            throw new InvalidOperationException("Only pending applications can be accepted.");
        }

        var business = await businesses.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Business profile is required.");
        if (application.Campaign.BusinessId != business.Id)
        {
            throw new UnauthorizedAccessException("You cannot accept an application for another business.");
        }

        if (await deals.ExistsForApplicationAsync(application.Id, cancellationToken))
        {
            throw new InvalidOperationException("A deal already exists for this application.");
        }

        application.Accept();
        var deal = Deal.Create(application.Id, application.BloggerId, business.Id);
        await deals.AddAsync(deal, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var blogger = bloggers is null ? null : await bloggers.GetByIdAsync(application.BloggerId, cancellationToken);
        if (blogger is not null) await BestEffortTelegramNotification.SendAsync(botClient, logger, blogger.TelegramUserId, $"BloggerBazar: ваша заявка на кампанию «{application.Campaign.Title}» принята.", cancellationToken);
        return DealDto.From(deal);
    }
}
