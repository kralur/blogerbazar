using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.CollaborationRequests;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Domain.Enums;
using BloggerBazar.Domain.Entities;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Admin;

public sealed record GetAdminBloggersQuery(long TelegramUserId, int Take = 100) : IRequest<IReadOnlyList<AdminBloggerProfileDto>>;

public sealed class GetAdminBloggersHandler(IAdminMarketplaceReadModel marketplace, IAdminAccessPolicy access) : IRequestHandler<GetAdminBloggersQuery, IReadOnlyList<AdminBloggerProfileDto>>
{
    public async Task<IReadOnlyList<AdminBloggerProfileDto>> Handle(GetAdminBloggersQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.TelegramUserId);
        return await marketplace.GetBloggersAsync(Math.Clamp(query.Take, 1, 100), cancellationToken);
    }
}

// Every company for the admin list, newest first; moderation status as the number the app already uses.
// TelegramUserId (added for the web admin) opens the owner's admin card.
public sealed record AdminBusinessDto(Guid Id, string Name, string? City, string? LogoUrl, int ModerationStatus, bool IsHidden, bool IsDeleted, int CampaignsCount, int CompletedDealsCount, DateTime CreatedAtUtc, long? TelegramUserId = null);

public sealed record GetAdminBusinessesQuery(long TelegramUserId, int Take = 100) : IRequest<IReadOnlyList<AdminBusinessDto>>;

public sealed class GetAdminBusinessesHandler(IAdminMarketplaceReadModel marketplace, IAdminAccessPolicy access) : IRequestHandler<GetAdminBusinessesQuery, IReadOnlyList<AdminBusinessDto>>
{
    public async Task<IReadOnlyList<AdminBusinessDto>> Handle(GetAdminBusinessesQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.TelegramUserId);
        return await marketplace.GetBusinessesAsync(Math.Clamp(query.Take, 1, 100), cancellationToken);
    }
}

public sealed record GetAdminCollaborationRequestsQuery(long TelegramUserId, int Take = 100) : IRequest<IReadOnlyList<CollaborationRequestDto>>;

public sealed class GetAdminCollaborationRequestsHandler(IAdminMarketplaceReadModel marketplace, IAdminAccessPolicy access) : IRequestHandler<GetAdminCollaborationRequestsQuery, IReadOnlyList<CollaborationRequestDto>>
{
    public async Task<IReadOnlyList<CollaborationRequestDto>> Handle(GetAdminCollaborationRequestsQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.TelegramUserId);
        return await marketplace.GetCollaborationRequestsAsync(Math.Clamp(query.Take, 1, 100), cancellationToken);
    }
}

public sealed record AdminCampaignDto(Guid Id, string Title, string BusinessName, int Status, bool IsPromoted, int ApplicationsCount, DateTime CreatedAtUtc, long? BusinessTelegramUserId = null)
{
    public static AdminCampaignDto From(Domain.Entities.Campaign campaign) =>
        new(campaign.Id, campaign.Title, campaign.Business.Name, (int)campaign.Status, campaign.IsPromoted, campaign.Applications.Count, campaign.CreatedAtUtc, campaign.Business.TelegramUserId);
}

public sealed record GetAdminCampaignsQuery(long TelegramUserId, int Take = 100) : IRequest<IReadOnlyList<AdminCampaignDto>>;

public sealed class GetAdminCampaignsHandler(IAdminMarketplaceReadModel marketplace, IAdminAccessPolicy access) : IRequestHandler<GetAdminCampaignsQuery, IReadOnlyList<AdminCampaignDto>>
{
    public async Task<IReadOnlyList<AdminCampaignDto>> Handle(GetAdminCampaignsQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.TelegramUserId);
        return await marketplace.GetCampaignsAsync(Math.Clamp(query.Take, 1, 100), cancellationToken);
    }
}

public sealed record ModerateCampaignCommand(Guid CampaignId, long TelegramUserId, CampaignStatus? Status, bool? IsPromoted) : IRequest<AdminCampaignDto>;

public sealed class ModerateCampaignValidator : AbstractValidator<ModerateCampaignCommand>
{
    public ModerateCampaignValidator()
    {
        RuleFor(command => command.CampaignId).NotEmpty();
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.Status).IsInEnum().When(command => command.Status.HasValue);
        RuleFor(command => command).Must(command => command.Status.HasValue || command.IsPromoted.HasValue)
            .WithMessage("Campaign status or promotion must be provided.");
    }
}

public sealed class ModerateCampaignHandler(ICampaignRepository campaigns, IAdminAccessPolicy access, IAuditLogRepository auditLogs, IUnitOfWork unitOfWork, ICatalogCache? cache = null) : IRequestHandler<ModerateCampaignCommand, AdminCampaignDto>
{
    public async Task<AdminCampaignDto> Handle(ModerateCampaignCommand command, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(command.TelegramUserId);
        var campaign = await campaigns.GetByIdAsync(command.CampaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign was not found.");
        if (command.Status.HasValue && command.Status.Value != campaign.Status)
        {
            // Moderation is visible in the journal like promotion (a rejection is what the owner cannot undo).
            campaign.SetStatus(command.Status.Value);
            await auditLogs.AddAsync(AuditLog.Create(command.TelegramUserId, "campaign.status-changed", "Campaign", campaign.Id.ToString(), command.Status.Value.ToString()), cancellationToken);
        }
        if (command.IsPromoted.HasValue)
        {
            campaign.SetPromotion(command.IsPromoted.Value);
            await auditLogs.AddAsync(AuditLog.Create(command.TelegramUserId, command.IsPromoted.Value ? "campaign.promoted" : "campaign.promotion-removed", "Campaign", campaign.Id.ToString()), cancellationToken);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (cache is not null) await CampaignCatalogCache.InvalidateAsync(cache, cancellationToken);
        return AdminCampaignDto.From(campaign);
    }
}

// Every deal for the web admin, newest first. Status is the number the app already uses; CreatorRole is "blogger" or
// "brandFace" (D26 string). The Telegram ids open the two people's admin cards.
public sealed record AdminDealDto(
    Guid Id,
    string? Title,
    bool FromOffer,
    string BusinessName,
    long BusinessTelegramUserId,
    string CreatorRole,
    string CreatorName,
    long CreatorTelegramUserId,
    int Status,
    int? AgreedPrice,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);

// Filter values for the admin deal list; null means every deal.
public static class AdminDealFilters
{
    public const string Active = "active";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    // Completed deals the money report misses: no price entered and no offer budget.
    public const string WithoutPrice = "withoutPrice";
    public static readonly IReadOnlyDictionary<string, DealStatus> Statuses = new Dictionary<string, DealStatus>(StringComparer.Ordinal)
    {
        [Active] = DealStatus.Active,
        [Completed] = DealStatus.Completed,
        [Cancelled] = DealStatus.Cancelled
    };
}

public sealed record GetAdminDealsQuery(long TelegramUserId, string? Status = null, int Take = 100) : IRequest<IReadOnlyList<AdminDealDto>>;

public sealed class GetAdminDealsValidator : AbstractValidator<GetAdminDealsQuery>
{
    public GetAdminDealsValidator()
    {
        RuleFor(query => query.Status).Must(status => status is null || status == AdminDealFilters.WithoutPrice || AdminDealFilters.Statuses.ContainsKey(status));
    }
}

public sealed class GetAdminDealsHandler(IAdminMarketplaceReadModel marketplace, IAdminAccessPolicy access) : IRequestHandler<GetAdminDealsQuery, IReadOnlyList<AdminDealDto>>
{
    public async Task<IReadOnlyList<AdminDealDto>> Handle(GetAdminDealsQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.TelegramUserId);
        var take = Math.Clamp(query.Take, 1, 200);
        if (query.Status == AdminDealFilters.WithoutPrice)
        {
            return await marketplace.GetDealsAsync(null, take, cancellationToken, withoutPrice: true);
        }

        DealStatus? status = query.Status is null ? null : AdminDealFilters.Statuses[query.Status];
        return await marketplace.GetDealsAsync(status, take, cancellationToken);
    }
}
