using BloggerBazar.Application.Features.Admin;
using BloggerBazar.Application.Features.CollaborationRequests;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IAdminMarketplaceReadModel
{
    Task<IReadOnlyList<AdminBloggerProfileDto>> GetBloggersAsync(int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminCampaignDto>> GetCampaignsAsync(int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminBusinessDto>> GetBusinessesAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AdminBusinessDto>>([]);
    Task<IReadOnlyList<CollaborationRequestDto>> GetCollaborationRequestsAsync(int take, CancellationToken cancellationToken);
    Task<AdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminPlatformUserDto>> SearchUsersAsync(string? search, string? filter, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AdminPlatformUserDto>>([]);
    Task<AdminUserDetailsDto?> GetUserDetailsAsync(long telegramUserId, CancellationToken cancellationToken) =>
        Task.FromResult<AdminUserDetailsDto?>(null);
    // People who first opened the app since fromUtc (all time when null) and the funnel steps they reached, per role.
    Task<IReadOnlyList<AdminFunnelRoleDto>> GetFunnelAsync(DateTime? fromUtc, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AdminFunnelRoleDto>>([]);
    // Deals newest first, every status when status is null.
    Task<IReadOnlyList<AdminDealDto>> GetDealsAsync(DealStatus? status, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AdminDealDto>>([]);
    // Completed deals still without a price (none entered, no offer budget) when withoutPrice is set.
    Task<IReadOnlyList<AdminDealDto>> GetDealsAsync(DealStatus? status, int take, CancellationToken cancellationToken, bool withoutPrice) =>
        withoutPrice ? Task.FromResult<IReadOnlyList<AdminDealDto>>([]) : GetDealsAsync(status, take, cancellationToken);
}
