using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Domain.Enums;
using MediatR;

namespace BloggerBazar.Application.Features.Admin;

public sealed record AdminDashboardDto(
    int Users,
    int Bloggers,
    int Businesses,
    int PublishedCampaigns,
    int CompletedDeals,
    int PromotedBloggers,
    int PromotedCampaigns);

public sealed record GetAdminDashboardQuery(long TelegramUserId) : IRequest<AdminDashboardDto>;

public sealed class GetAdminDashboardHandler(
    IAdminAccessPolicy access,
    IAdminMarketplaceReadModel marketplace) : IRequestHandler<GetAdminDashboardQuery, AdminDashboardDto>
{
    public async Task<AdminDashboardDto> Handle(GetAdminDashboardQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.TelegramUserId);
        return await marketplace.GetDashboardAsync(cancellationToken);
    }
}
