using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

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
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateCampaignApplicationStatusCommand, MyCampaignApplicationDto>
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

        if (command.Status == CampaignApplicationStatus.Viewed)
        {
            application.MarkViewed();
        }
        else
        {
            application.Reject();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MyCampaignApplicationDto.ForBusiness(application);
    }
}
