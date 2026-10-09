using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Notifications;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.Campaigns;

public sealed record UpdateCampaignApplicationStatusCommand(Guid ApplicationId, long TelegramUserId, CampaignApplicationStatus Status) : IRequest<MyCampaignApplicationDto>;

public sealed class UpdateCampaignApplicationStatusValidator : AbstractValidator<UpdateCampaignApplicationStatusCommand>
{
    public UpdateCampaignApplicationStatusValidator()
    {
        RuleFor(command => command.ApplicationId).NotEmpty();
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.Status).IsInEnum();
        RuleFor(command => command.Status).Must(status => status is CampaignApplicationStatus.Viewed or CampaignApplicationStatus.Rejected)
            .WithMessage("Only viewed and rejected statuses can be set directly.");
    }
}

public sealed class UpdateCampaignApplicationStatusHandler(
    ICampaignApplicationRepository applications,
    IBusinessProfileRepository businesses,
    IUnitOfWork unitOfWork,
    IBloggerProfileRepository bloggers,
    ITelegramBotClient? botClient = null,
    ILogger<UpdateCampaignApplicationStatusHandler>? logger = null,
    IBrandFaceProfileRepository? brandFaces = null) : IRequestHandler<UpdateCampaignApplicationStatusCommand, MyCampaignApplicationDto>
{
    public async Task<MyCampaignApplicationDto> Handle(UpdateCampaignApplicationStatusCommand command, CancellationToken cancellationToken)
    {
        var application = await applications.GetByIdAsync(command.ApplicationId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign application was not found.");
        var business = await businesses.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Business profile is required.");
        if (application.Campaign.BusinessId != business.Id)
        {
            throw new UnauthorizedAccessException("You cannot update an application for another business.");
        }
        var applicant = await FindApplicantAsync(application, cancellationToken)
            ?? throw new InvalidOperationException("Blogger profile was not found.");

        if (command.Status == CampaignApplicationStatus.Viewed)
        {
            if (!CampaignApplicationLifecycle.IsPending(application.Status))
            {
                throw new InvalidOperationException("Campaign application status conflicts with its current status.");
            }
            application.MarkViewed();
        }
        else
        {
            if (application.Status != CampaignApplicationStatus.Rejected)
            {
                if (!CampaignApplicationLifecycle.IsPending(application.Status))
                {
                    throw new InvalidOperationException("Campaign application status conflicts with its current status.");
                }
                application.Reject();
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (command.Status == CampaignApplicationStatus.Rejected)
        {
            await BestEffortTelegramNotification.SendAsync(botClient, logger, applicant.TelegramUserId, BotMessages.CampaignApplicationRejected(application.Campaign.Title), $"/my-application/{application.Id}", cancellationToken);
        }
        return MyCampaignApplicationDto.ForBusiness(application, applicant.Id, applicant.Name, applicant.ImageUrl);
    }

    private async Task<Applicant?> FindApplicantAsync(Domain.Entities.CampaignApplication application, CancellationToken cancellationToken)
    {
        if (application.BrandFaceId is { } brandFaceId)
        {
            return brandFaces is not null && await brandFaces.GetByIdAsync(brandFaceId, cancellationToken) is { } brandFace
                ? new Applicant(brandFace.Id, brandFace.TelegramUserId, brandFace.Name, brandFace.AvatarUrl)
                : null;
        }

        return application.BloggerId is { } bloggerId && await bloggers.GetByIdAsync(bloggerId, cancellationToken) is { } blogger
            ? new Applicant(blogger.Id, blogger.TelegramUserId, blogger.Name, blogger.AvatarUrl)
            : null;
    }

    private sealed record Applicant(Guid Id, long TelegramUserId, string Name, string? ImageUrl);
}
