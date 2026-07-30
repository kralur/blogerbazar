using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.CollaborationRequests;
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

public sealed record GetAdminCollaborationRequestsQuery(long TelegramUserId, int Take = 100) : IRequest<IReadOnlyList<CollaborationRequestDto>>;

public sealed class GetAdminCollaborationRequestsHandler(IAdminMarketplaceReadModel marketplace, IAdminAccessPolicy access) : IRequestHandler<GetAdminCollaborationRequestsQuery, IReadOnlyList<CollaborationRequestDto>>
{
    public async Task<IReadOnlyList<CollaborationRequestDto>> Handle(GetAdminCollaborationRequestsQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.TelegramUserId);
        return await marketplace.GetCollaborationRequestsAsync(Math.Clamp(query.Take, 1, 100), cancellationToken);
    }
}

public sealed record AdminCampaignDto(Guid Id, string Title, string BusinessName, int Status, bool IsPromoted, int ApplicationsCount, DateTime CreatedAtUtc)
{
    public static AdminCampaignDto From(Domain.Entities.Campaign campaign) =>
        new(campaign.Id, campaign.Title, campaign.Business.Name, (int)campaign.Status, campaign.IsPromoted, campaign.Applications.Count, campaign.CreatedAtUtc);
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
        RuleFor(command => command).Must(command => command.Status.HasValue || command.IsPromoted.HasValue)
            .WithMessage("Campaign status or promotion must be provided.");
    }
}

public sealed class ModerateCampaignHandler(ICampaignRepository campaigns, IAdminAccessPolicy access, IAuditLogRepository auditLogs, IUnitOfWork unitOfWork) : IRequestHandler<ModerateCampaignCommand, AdminCampaignDto>
{
    public async Task<AdminCampaignDto> Handle(ModerateCampaignCommand command, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(command.TelegramUserId);
        var campaign = await campaigns.GetByIdAsync(command.CampaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign was not found.");
        if (command.Status.HasValue) campaign.SetStatus(command.Status.Value);
        if (command.IsPromoted.HasValue)
        {
            campaign.SetPromotion(command.IsPromoted.Value);
            await auditLogs.AddAsync(AuditLog.Create(command.TelegramUserId, command.IsPromoted.Value ? "campaign.promoted" : "campaign.promotion-removed", "Campaign", campaign.Id.ToString()), cancellationToken);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AdminCampaignDto.From(campaign);
    }
}
