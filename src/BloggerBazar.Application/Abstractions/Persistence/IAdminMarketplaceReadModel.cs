using BloggerBazar.Application.Features.Admin;
using BloggerBazar.Application.Features.CollaborationRequests;

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
}
