using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Campaigns;

public sealed record CreateCampaignCommand(long TelegramUserId, string Title, string Description, string? City, IReadOnlyCollection<string> Categories, int? BudgetFrom, int? BudgetTo, bool PublishImmediately) : IRequest<CampaignDto>;

public sealed class CreateCampaignValidator : AbstractValidator<CreateCampaignCommand>
{
    public CreateCampaignValidator()
    {
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.Title).NotEmpty().MaximumLength(160);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(3000);
        RuleFor(command => command.City).MaximumLength(80).When(command => command.City is not null);
        RuleFor(command => command.Categories).NotEmpty().Must(categories => categories.Count <= 5);
        RuleForEach(command => command.Categories).NotEmpty().MaximumLength(50);
        RuleFor(command => command.BudgetFrom).GreaterThanOrEqualTo(0).When(command => command.BudgetFrom.HasValue);
        RuleFor(command => command.BudgetTo).GreaterThanOrEqualTo(command => command.BudgetFrom!.Value).When(command => command.BudgetFrom.HasValue && command.BudgetTo.HasValue);
    }
}

public sealed class CreateCampaignHandler(IBusinessProfileRepository businesses, ICampaignRepository campaigns, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateCampaignCommand, CampaignDto>
{
    public async Task<CampaignDto> Handle(CreateCampaignCommand command, CancellationToken cancellationToken)
    {
        var business = await businesses.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken)
            ?? throw new InvalidOperationException("Create a business profile before publishing a campaign.");
        var campaign = Campaign.Create(business.Id, command.Title.Trim(), command.Description.Trim(), command.Categories.Select(category => category.Trim()).ToArray(), command.BudgetFrom, command.BudgetTo, command.City?.Trim());
        if (command.PublishImmediately)
        {
            campaign.Publish();
        }

        await campaigns.AddAsync(campaign, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CampaignDto.From(campaign, business.Name);
    }
}
