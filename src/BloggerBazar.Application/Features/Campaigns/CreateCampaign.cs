using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Domain.Entities;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Campaigns;

public sealed record CreateCampaignCommand(long TelegramUserId, string Title, string Description, string? City, IReadOnlyCollection<string> Categories, IReadOnlyCollection<string>? Requirements, int? BudgetFrom, int? BudgetTo, DateTime? Deadline, bool PublishImmediately) : IRequest<CampaignDto>;

public sealed class CreateCampaignValidator : AbstractValidator<CreateCampaignCommand>
{
    public CreateCampaignValidator()
    {
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.Title).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(160);
        RuleFor(command => command.Description).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(3000);
        RuleFor(command => command.City).MaximumLength(80).When(command => command.City is not null);
        RuleFor(command => command.Categories).NotEmpty().Must(categories => categories.Count <= 5);
        RuleForEach(command => command.Categories).Must(category => !string.IsNullOrWhiteSpace(category)).MaximumLength(50);
        RuleFor(command => command.Requirements).Must(requirements => requirements is null || requirements.Count <= 10);
        RuleForEach(command => command.Requirements!).Must(requirement => !string.IsNullOrWhiteSpace(requirement)).MaximumLength(300).When(command => command.Requirements is not null);
        RuleFor(command => command.BudgetFrom).GreaterThanOrEqualTo(0).When(command => command.BudgetFrom.HasValue);
        RuleFor(command => command.BudgetTo).GreaterThanOrEqualTo(0).When(command => command.BudgetTo.HasValue);
        RuleFor(command => command.BudgetTo).GreaterThanOrEqualTo(command => command.BudgetFrom!.Value).When(command => command.BudgetFrom.HasValue && command.BudgetTo.HasValue);
    }
}

public sealed class CreateCampaignHandler(IBusinessProfileRepository businesses, ICampaignRepository campaigns, IUnitOfWork unitOfWork, ICatalogCache? cache = null)
    : IRequestHandler<CreateCampaignCommand, CampaignDto>
{
    public async Task<CampaignDto> Handle(CreateCampaignCommand command, CancellationToken cancellationToken)
    {
        var business = await businesses.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken)
            ?? throw new InvalidOperationException("Create a business profile before publishing a campaign.");
        var campaign = Campaign.Create(business.Id, command.Title.Trim(), command.Description.Trim(), command.Categories.Select(category => category.Trim()).ToArray(), command.Requirements?.Select(requirement => requirement.Trim()).ToArray(), command.BudgetFrom, command.BudgetTo, NormalizeOptional(command.City), command.Deadline?.ToUniversalTime());
        if (command.PublishImmediately)
        {
            campaign.Publish();
        }

        await campaigns.AddAsync(campaign, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (cache is not null) await CampaignCatalogCache.InvalidateAsync(cache, cancellationToken);
        return CampaignDto.From(campaign, business.Name);
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
