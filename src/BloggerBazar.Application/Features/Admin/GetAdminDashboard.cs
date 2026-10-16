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
    int PromotedCampaigns,
    int OpenComplaints = 0,
    int NewUsersToday = 0,
    int NewUsersWeek = 0,
    int NewDealsWeek = 0,
    int CompletedDealsWeek = 0,
    // People who opened the app today (UTC day) and in the last 7 days.
    int ActiveToday = 0,
    int ActiveWeek = 0,
    // Accounts per ad label (t.me/bot?start=<label>), the ten biggest.
    IReadOnlyList<AdminSourceDto>? Sources = null,
    // The admin site (AdminWeb:Origin), so the Mini App admin can link to the full admin; null while it is off.
    string? AdminWebUrl = null);

public sealed record AdminSourceDto(string Source, int Users);

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
