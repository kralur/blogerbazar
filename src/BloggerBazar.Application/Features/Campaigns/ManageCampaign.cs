using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Campaigns;

public sealed record UpdateCampaignCommand(Guid CampaignId, long TelegramUserId, string Title, string Description, string? City, IReadOnlyCollection<string> Categories, IReadOnlyCollection<string>? Requirements, int? BudgetFrom, int? BudgetTo, DateTime? Deadline, bool PublishImmediately) : IRequest<CampaignDto>;
public sealed record CloseCampaignCommand(Guid CampaignId, long TelegramUserId) : IRequest<CampaignDto>;

public sealed class UpdateCampaignValidator : AbstractValidator<UpdateCampaignCommand>
{
    public UpdateCampaignValidator()
    {
        RuleFor(command => command.CampaignId).NotEmpty(); RuleFor(command => command.TelegramUserId).GreaterThan(0); RuleFor(command => command.Title).NotEmpty().MaximumLength(160); RuleFor(command => command.Description).NotEmpty().MaximumLength(3000); RuleFor(command => command.Categories).NotEmpty().Must(categories => categories.Count <= 5); RuleForEach(command => command.Categories).NotEmpty().MaximumLength(50); RuleFor(command => command.Requirements).Must(requirements => requirements is null || requirements.Count <= 10); RuleFor(command => command.BudgetTo).GreaterThanOrEqualTo(command => command.BudgetFrom!.Value).When(command => command.BudgetFrom.HasValue && command.BudgetTo.HasValue);
    }
}

public sealed class UpdateCampaignHandler(ICampaignRepository campaigns, IBusinessProfileRepository businesses, IUnitOfWork unitOfWork, ICatalogCache? cache = null) : IRequestHandler<UpdateCampaignCommand, CampaignDto>
{
    public async Task<CampaignDto> Handle(UpdateCampaignCommand command, CancellationToken cancellationToken)
    {
        var business = await businesses.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken) ?? throw new UnauthorizedAccessException("Business profile is required.");
        var campaign = await campaigns.GetByIdAsync(command.CampaignId, cancellationToken) ?? throw new InvalidOperationException("Campaign was not found.");
        if (campaign.BusinessId != business.Id) throw new UnauthorizedAccessException("You cannot edit another business campaign.");
        if (campaign.Status == CampaignStatus.Archived) throw new InvalidOperationException("Archived campaigns cannot be edited.");
        campaign.Update(command.Title.Trim(), command.Description.Trim(), command.Categories.Select(category => category.Trim()).ToArray(), command.Requirements?.Select(requirement => requirement.Trim()).ToArray(), command.BudgetFrom, command.BudgetTo, command.City?.Trim(), command.Deadline?.ToUniversalTime());
        if (command.PublishImmediately && campaign.Status == CampaignStatus.Draft) campaign.Publish();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (cache is not null) await CampaignCatalogCache.InvalidateAsync(cache, cancellationToken);
        return CampaignDto.From(campaign, business.Name);
    }
}

public sealed class CloseCampaignHandler(ICampaignRepository campaigns, IBusinessProfileRepository businesses, IUnitOfWork unitOfWork, ICatalogCache? cache = null) : IRequestHandler<CloseCampaignCommand, CampaignDto>
{
    public async Task<CampaignDto> Handle(CloseCampaignCommand command, CancellationToken cancellationToken)
    {
        var business = await businesses.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken) ?? throw new UnauthorizedAccessException("Business profile is required.");
        var campaign = await campaigns.GetByIdAsync(command.CampaignId, cancellationToken) ?? throw new InvalidOperationException("Campaign was not found.");
        if (campaign.BusinessId != business.Id) throw new UnauthorizedAccessException("You cannot close another business campaign.");
        if (campaign.Status != CampaignStatus.Archived) campaign.Archive();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (cache is not null) await CampaignCatalogCache.InvalidateAsync(cache, cancellationToken);
        return CampaignDto.From(campaign, business.Name);
    }
}
