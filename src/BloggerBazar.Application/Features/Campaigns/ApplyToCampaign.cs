using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Campaigns;

public sealed record ApplyToCampaignCommand(Guid CampaignId, long TelegramUserId, string? Message) : IRequest<CampaignApplicationDto>;

public sealed record CampaignApplicationDto(Guid Id, Guid CampaignId, Guid BloggerId, string? Message, int Status, DateTime CreatedAtUtc)
{
    public static CampaignApplicationDto From(CampaignApplication application) => new(application.Id, application.CampaignId, application.BloggerId, application.Message, (int)application.Status, application.CreatedAtUtc);
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
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    ICampaignApplicationRepository applications,
    IUnitOfWork unitOfWork) : IRequestHandler<ApplyToCampaignCommand, CampaignApplicationDto>
{
    public async Task<CampaignApplicationDto> Handle(ApplyToCampaignCommand command, CancellationToken cancellationToken)
    {
        var campaign = await campaigns.GetByIdAsync(command.CampaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign was not found.");
        if (campaign.Status != CampaignStatus.Published)
        {
            throw new InvalidOperationException("Applications are available only for published campaigns.");
        }

        var blogger = await bloggers.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken)
            ?? throw new InvalidOperationException("Create a blogger profile before applying to campaigns.");
        if (blogger.Status != BloggerStatus.Approved)
        {
            throw new InvalidOperationException("Only approved blogger profiles can apply to campaigns.");
        }

        var business = await businesses.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken);
        if (business?.Id == campaign.BusinessId)
        {
            throw new InvalidOperationException("You cannot apply to your own campaign.");
        }

        if (await applications.ExistsAsync(campaign.Id, blogger.Id, cancellationToken))
        {
            throw new InvalidOperationException("You have already applied to this campaign.");
        }

        var application = CampaignApplication.Create(campaign.Id, blogger.Id, command.Message?.Trim());
        await applications.AddAsync(application, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CampaignApplicationDto.From(application);
    }
}
