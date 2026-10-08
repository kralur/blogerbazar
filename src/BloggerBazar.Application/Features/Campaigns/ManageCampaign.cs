using BloggerBazar.Application.Validation;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Campaigns;

public sealed record UpdateCampaignCommand(Guid CampaignId, long TelegramUserId, string Title, string Description, string? City, IReadOnlyCollection<string> Categories, IReadOnlyCollection<string>? Requirements, int? BudgetFrom, int? BudgetTo, DateTime? Deadline) : IRequest<CampaignDto>;
public sealed record CloseCampaignCommand(Guid CampaignId, long TelegramUserId) : IRequest<CampaignDto>;
public sealed record ReopenCampaignCommand(Guid CampaignId, long TelegramUserId) : IRequest<CampaignDto>;
public sealed record DeleteCampaignCommand(Guid CampaignId, long TelegramUserId) : IRequest;

public sealed class UpdateCampaignValidator : AbstractValidator<UpdateCampaignCommand>
{
    public UpdateCampaignValidator()
    {
        RuleFor(command => command.CampaignId).NotEmpty(); RuleFor(command => command.TelegramUserId).GreaterThan(0); RuleFor(command => command.Title).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(160); RuleFor(command => command.Description).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(3000); RuleFor(command => command.City).MaximumLength(80).Must(Regions.IsKnown).When(command => !string.IsNullOrWhiteSpace(command.City)); RuleFor(command => command.Categories).NotEmpty().Must(categories => categories.Count <= 5); RuleForEach(command => command.Categories).Must(category => !string.IsNullOrWhiteSpace(category)).MaximumLength(50); RuleFor(command => command.Requirements).Must(requirements => requirements is null || requirements.Count <= 10); RuleForEach(command => command.Requirements!).Must(requirement => !string.IsNullOrWhiteSpace(requirement)).MaximumLength(300).When(command => command.Requirements is not null); RuleFor(command => command.BudgetFrom).GreaterThanOrEqualTo(0).LessThanOrEqualTo(InputLimits.MaxMoney).When(command => command.BudgetFrom.HasValue); RuleFor(command => command.BudgetTo).GreaterThanOrEqualTo(0).LessThanOrEqualTo(InputLimits.MaxMoney).When(command => command.BudgetTo.HasValue); RuleFor(command => command.BudgetTo).GreaterThanOrEqualTo(command => command.BudgetFrom!.Value).When(command => command.BudgetFrom.HasValue && command.BudgetTo.HasValue); RuleFor(command => command.Deadline).GreaterThan(_ => DateTime.UtcNow.AddDays(-1)).When(command => command.Deadline.HasValue);
    }
}

public sealed class UpdateCampaignHandler(ICampaignRepository campaigns, IPlatformUserRepository users, IBusinessProfileRepository businesses, IUnitOfWork unitOfWork, ICatalogCache? cache = null) : IRequestHandler<UpdateCampaignCommand, CampaignDto>
{
    public async Task<CampaignDto> Handle(UpdateCampaignCommand command, CancellationToken cancellationToken)
    {
        var business = await CampaignManagementAccess.RequireBusinessAsync(users, businesses, command.TelegramUserId, cancellationToken);
        var campaign = await campaigns.GetByIdForBusinessAsync(command.CampaignId, business.Id, cancellationToken) ?? throw new InvalidOperationException("Campaign was not found.");
        if (campaign.Status == CampaignStatus.Archived) throw new InvalidOperationException("Archived campaigns cannot be edited.");
        campaign.Update(command.Title.Trim(), command.Description.Trim(), command.Categories.Select(category => category.Trim()).ToArray(), command.Requirements?.Select(requirement => requirement.Trim()).ToArray(), command.BudgetFrom, command.BudgetTo, NormalizeOptional(command.City), command.Deadline?.ToUniversalTime());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (cache is not null) await CampaignCatalogCache.InvalidateAsync(cache, cancellationToken);
        return CampaignDto.From(campaign, business.Name);
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CloseCampaignHandler(ICampaignRepository campaigns, IPlatformUserRepository users, IBusinessProfileRepository businesses, IUnitOfWork unitOfWork, ICatalogCache? cache = null) : IRequestHandler<CloseCampaignCommand, CampaignDto>
{
    public async Task<CampaignDto> Handle(CloseCampaignCommand command, CancellationToken cancellationToken)
    {
        var business = await CampaignManagementAccess.RequireBusinessAsync(users, businesses, command.TelegramUserId, cancellationToken);
        var campaign = await campaigns.GetByIdForBusinessAsync(command.CampaignId, business.Id, cancellationToken) ?? throw new InvalidOperationException("Campaign was not found.");
        if (campaign.Status != CampaignStatus.Archived)
        {
            campaign.Archive();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            if (cache is not null) await CampaignCatalogCache.InvalidateAsync(cache, cancellationToken);
        }

        return CampaignDto.From(campaign, business.Name);
    }
}

public sealed class ReopenCampaignHandler(ICampaignRepository campaigns, IPlatformUserRepository users, IBusinessProfileRepository businesses, IUnitOfWork unitOfWork, ICatalogCache? cache = null) : IRequestHandler<ReopenCampaignCommand, CampaignDto>
{
    public async Task<CampaignDto> Handle(ReopenCampaignCommand command, CancellationToken cancellationToken)
    {
        var business = await CampaignManagementAccess.RequireBusinessAsync(users, businesses, command.TelegramUserId, cancellationToken);
        var campaign = await campaigns.GetByIdForBusinessAsync(command.CampaignId, business.Id, cancellationToken) ?? throw new InvalidOperationException("Campaign was not found.");
        if (campaign.Status != CampaignStatus.Published)
        {
            // A campaign past its deadline would reopen straight into "expired"; the owner moves the deadline first.
            if (campaign.IsExpired(DateTime.UtcNow)) throw new BusinessRuleConflictException("campaign_expired", "The campaign deadline has passed.");
            campaign.Publish();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            if (cache is not null) await CampaignCatalogCache.InvalidateAsync(cache, cancellationToken);
        }

        return CampaignDto.From(campaign, business.Name);
    }
}

public sealed class DeleteCampaignHandler(ICampaignRepository campaigns, IPlatformUserRepository users, IBusinessProfileRepository businesses, ICatalogCache? cache = null) : IRequestHandler<DeleteCampaignCommand>
{
    public async Task Handle(DeleteCampaignCommand command, CancellationToken cancellationToken)
    {
        var business = await CampaignManagementAccess.RequireBusinessAsync(users, businesses, command.TelegramUserId, cancellationToken);
        _ = await campaigns.GetByIdForBusinessAsync(command.CampaignId, business.Id, cancellationToken) ?? throw new InvalidOperationException("Campaign was not found.");
        // Applications carry bloggers' history and deals, so a campaign with any of them is closed, never deleted.
        if (!await campaigns.DeleteWithoutApplicationsAsync(command.CampaignId, business.Id, cancellationToken))
            throw new BusinessRuleConflictException("campaign_has_applications", "A campaign with applications cannot be deleted.");
        if (cache is not null) await CampaignCatalogCache.InvalidateAsync(cache, cancellationToken);
    }
}
