using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Enums;
using MediatR;

namespace BloggerBazar.Application.Features.Campaigns;

public sealed record GetMyCampaignApplicationsQuery(long TelegramUserId) : IRequest<IReadOnlyList<MyCampaignApplicationDto>>;

// CounterpartyRole: "business", "blogger" or "brandFace", the kind of profile CounterpartyProfileId opens (D46).
public sealed record MyCampaignApplicationDto(Guid Id, Guid CampaignId, string CampaignTitle, string CounterpartyName, string? CounterpartyImageUrl, string? Message, int Status, bool CanAccept, DateTime CreatedAtUtc, Guid? CounterpartyProfileId = null, string? CounterpartyRole = null)
{
    public const string BusinessCounterparty = "business";

    public static MyCampaignApplicationDto ForBlogger(Domain.Entities.CampaignApplication application) =>
        new(application.Id, application.CampaignId, application.Campaign.Title, application.Campaign.Business.Name, application.Campaign.Business.LogoUrl, application.Message, (int)application.Status, false, application.CreatedAtUtc, application.Campaign.BusinessId, BusinessCounterparty);

    public static MyCampaignApplicationDto ForBusiness(Domain.Entities.CampaignApplication application, Domain.Entities.BloggerProfile blogger) =>
        ForBusiness(application, blogger.Id, blogger.Name, blogger.AvatarUrl);

    public static MyCampaignApplicationDto ForBusiness(Domain.Entities.CampaignApplication application, Guid applicantId, string applicantName, string? applicantImageUrl) =>
        new(application.Id, application.CampaignId, application.Campaign.Title, applicantName, applicantImageUrl, application.Message, (int)application.Status, true, application.CreatedAtUtc, applicantId, CreatorRoles.Of(application.CreatorRole));
}

public sealed class GetMyCampaignApplicationsHandler(
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IMarketplaceCatalogReadModel catalog,
    IBrandFaceProfileRepository? brandFaces = null,
    IPlatformUserRepository? users = null) : IRequestHandler<GetMyCampaignApplicationsQuery, IReadOnlyList<MyCampaignApplicationDto>>
{
    // Only the selected role's side: an account with a brand face (or blogger) and a business profile must not
    // see its own outgoing applications in the business inbox as if someone had applied (QA Q15).
    public async Task<IReadOnlyList<MyCampaignApplicationDto>> Handle(GetMyCampaignApplicationsQuery query, CancellationToken cancellationToken)
    {
        var role = users is null ? null : (await users.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken))?.SelectedMarketplaceRole;
        // No selected role means no side at all, never the union of every profile (selected-role rule).
        if (users is not null && role is null)
        {
            return [];
        }

        var blogger = role is null or MarketplaceRole.Blogger ? await bloggers.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken) : null;
        var business = role is null or MarketplaceRole.Business ? await businesses.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken) : null;
        var brandFace = brandFaces is not null && role == MarketplaceRole.BrandFace ? await brandFaces.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken) : null;
        return await catalog.GetCampaignApplicationsAsync(blogger?.Id, business?.Id, cancellationToken, brandFace?.Id);
    }
}
