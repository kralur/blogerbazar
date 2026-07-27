using BloggerBazar.Application.Abstractions.Persistence;
using MediatR;

namespace BloggerBazar.Application.Features.Campaigns;

public sealed record GetMyCampaignApplicationsQuery(long TelegramUserId) : IRequest<IReadOnlyList<MyCampaignApplicationDto>>;

public sealed record MyCampaignApplicationDto(Guid Id, Guid CampaignId, string CampaignTitle, string CounterpartyName, string? Message, int Status, bool CanAccept, DateTime CreatedAtUtc)
{
    public static MyCampaignApplicationDto ForBlogger(Domain.Entities.CampaignApplication application) =>
        new(application.Id, application.CampaignId, application.Campaign.Title, application.Campaign.Business.Name, application.Message, (int)application.Status, false, application.CreatedAtUtc);

    public static MyCampaignApplicationDto ForBusiness(Domain.Entities.CampaignApplication application) =>
        new(application.Id, application.CampaignId, application.Campaign.Title, application.Blogger.Name, application.Message, (int)application.Status, true, application.CreatedAtUtc);
}

public sealed class GetMyCampaignApplicationsHandler(
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    ICampaignApplicationRepository applications) : IRequestHandler<GetMyCampaignApplicationsQuery, IReadOnlyList<MyCampaignApplicationDto>>
{
    public async Task<IReadOnlyList<MyCampaignApplicationDto>> Handle(GetMyCampaignApplicationsQuery query, CancellationToken cancellationToken)
    {
        var blogger = await bloggers.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken);
        var business = await businesses.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken);
        var response = new List<MyCampaignApplicationDto>();
        if (blogger is not null) response.AddRange((await applications.GetForBloggerAsync(blogger.Id, cancellationToken)).Select(MyCampaignApplicationDto.ForBlogger));
        if (business is not null) response.AddRange((await applications.GetForBusinessAsync(business.Id, cancellationToken)).Select(MyCampaignApplicationDto.ForBusiness));
        return response.OrderByDescending(application => application.CreatedAtUtc).ToArray();
    }
}
