using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Application.Notifications;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.Campaigns;

public sealed record CampaignApplicationSearch(int? Status, int Page, int PageSize, Guid? CampaignId = null);

public sealed record MyCampaignApplicationsResult(
    IReadOnlyList<MyCampaignApplicationItemDto> Items,
    int Total,
    int Page,
    int PageSize,
    bool HasMore);

public sealed record MyCampaignApplicationItemDto(
    Guid Id,
    Guid CampaignId,
    string CampaignTitle,
    string BusinessName,
    string? BusinessAvatarUrl,
    string? City,
    IReadOnlyCollection<string> Categories,
    int? MinBudget,
    int? MaxBudget,
    DateTime? Deadline,
    string? Message,
    int Status,
    DateTime CreatedAtUtc,
    Guid? DealId);

public sealed record MyCampaignApplicationDetailsDto(
    Guid Id,
    Guid CampaignId,
    string CampaignTitle,
    string CampaignDescription,
    string BusinessName,
    string? BusinessAvatarUrl,
    string? City,
    IReadOnlyCollection<string> Categories,
    IReadOnlyCollection<string> Requirements,
    int? MinBudget,
    int? MaxBudget,
    DateTime? Deadline,
    string? Message,
    int Status,
    DateTime CreatedAtUtc,
    Guid? DealId,
    Guid? BusinessId = null);

public sealed record CampaignApplicationInboxResult(
    IReadOnlyList<CampaignApplicationInboxItemDto> Items,
    int Total,
    int Page,
    int PageSize,
    bool HasMore);

// The applicant is a blogger or a brand face (D46): BloggerId or BrandFaceId is set and CreatorRole names which.
// The "Blogger*" field names stay for the existing frontend and carry the applicant of either kind.
public sealed record CampaignApplicationInboxItemDto(
    Guid Id,
    Guid? BloggerId,
    string BloggerName,
    string? BloggerAvatarUrl,
    string City,
    IReadOnlyCollection<string> Categories,
    string? Message,
    int Status,
    DateTime CreatedAtUtc,
    Guid? DealId,
    Guid? BrandFaceId = null,
    string CreatorRole = CreatorRoles.Blogger);

public sealed record CampaignApplicationDecisionDto(Guid Id, int Status, Guid? DealId);

public sealed record GetMyCampaignApplicationsPageQuery(long TelegramUserId, int? Status, int Page, int PageSize, Guid? CampaignId = null)
    : IRequest<MyCampaignApplicationsResult>;

public sealed record GetMyCampaignApplicationDetailsQuery(long TelegramUserId, Guid ApplicationId)
    : IRequest<MyCampaignApplicationDetailsDto?>;

public sealed record GetCampaignApplicationInboxQuery(long TelegramUserId, Guid CampaignId, int? Status, int Page, int PageSize)
    : IRequest<CampaignApplicationInboxResult>;

public sealed record WithdrawMyCampaignApplicationCommand(long TelegramUserId, Guid ApplicationId)
    : IRequest<CampaignApplicationDecisionDto>;

public sealed record DecideCampaignApplicationCommand(long TelegramUserId, Guid CampaignId, Guid ApplicationId, CampaignApplicationStatus Decision)
    : IRequest<CampaignApplicationDecisionDto>;

public sealed class CampaignApplicationSearchValidator : AbstractValidator<CampaignApplicationSearch>
{
    public CampaignApplicationSearchValidator()
    {
        RuleFor(search => search.Status)
            .Must(status => !status.HasValue || Enum.IsDefined((CampaignApplicationStatus)status.Value))
            .WithMessage("The campaign application status is invalid.");
        RuleFor(search => search.CampaignId)
            .Must(campaignId => !campaignId.HasValue || campaignId.Value != Guid.Empty)
            .WithMessage("The campaign identifier is invalid.");
        RuleFor(search => search.Page).InclusiveBetween(1, 100_000);
        RuleFor(search => search.PageSize).InclusiveBetween(1, 50);
    }
}

public sealed class GetMyCampaignApplicationsPageValidator : AbstractValidator<GetMyCampaignApplicationsPageQuery>
{
    public GetMyCampaignApplicationsPageValidator()
    {
        RuleFor(query => query.TelegramUserId).GreaterThan(0);
        RuleFor(query => new CampaignApplicationSearch(query.Status, query.Page, query.PageSize, query.CampaignId)).SetValidator(new CampaignApplicationSearchValidator());
    }
}

public sealed class GetMyCampaignApplicationDetailsValidator : AbstractValidator<GetMyCampaignApplicationDetailsQuery>
{
    public GetMyCampaignApplicationDetailsValidator()
    {
        RuleFor(query => query.TelegramUserId).GreaterThan(0);
        RuleFor(query => query.ApplicationId).NotEmpty();
    }
}

public sealed class GetCampaignApplicationInboxValidator : AbstractValidator<GetCampaignApplicationInboxQuery>
{
    public GetCampaignApplicationInboxValidator()
    {
        RuleFor(query => query.TelegramUserId).GreaterThan(0);
        RuleFor(query => query.CampaignId).NotEmpty();
        RuleFor(query => new CampaignApplicationSearch(query.Status, query.Page, query.PageSize, null)).SetValidator(new CampaignApplicationSearchValidator());
    }
}

public sealed class WithdrawMyCampaignApplicationValidator : AbstractValidator<WithdrawMyCampaignApplicationCommand>
{
    public WithdrawMyCampaignApplicationValidator()
    {
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.ApplicationId).NotEmpty();
    }
}

public sealed class DecideCampaignApplicationValidator : AbstractValidator<DecideCampaignApplicationCommand>
{
    public DecideCampaignApplicationValidator()
    {
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.CampaignId).NotEmpty();
        RuleFor(command => command.ApplicationId).NotEmpty();
        RuleFor(command => command.Decision).Must(decision => decision is CampaignApplicationStatus.Accepted or CampaignApplicationStatus.Rejected)
            .WithMessage("Only accepted and rejected decisions are supported.");
    }
}

public sealed class GetMyCampaignApplicationsPageHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    ICampaignApplicationReadModel applications,
    IBrandFaceProfileRepository? brandFaces = null) : IRequestHandler<GetMyCampaignApplicationsPageQuery, MyCampaignApplicationsResult>
{
    public async Task<MyCampaignApplicationsResult> Handle(GetMyCampaignApplicationsPageQuery query, CancellationToken cancellationToken)
    {
        var creator = await CampaignApplicationAccess.RequireCreatorAsync(users, bloggers, brandFaces, query.TelegramUserId, cancellationToken);
        return await applications.SearchForCreatorAsync(creator.Role, creator.ProfileId, new CampaignApplicationSearch(query.Status, query.Page, query.PageSize, query.CampaignId), cancellationToken);
    }
}

public sealed class GetMyCampaignApplicationDetailsHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    ICampaignApplicationReadModel applications,
    IBrandFaceProfileRepository? brandFaces = null) : IRequestHandler<GetMyCampaignApplicationDetailsQuery, MyCampaignApplicationDetailsDto?>
{
    public async Task<MyCampaignApplicationDetailsDto?> Handle(GetMyCampaignApplicationDetailsQuery query, CancellationToken cancellationToken)
    {
        var creator = await CampaignApplicationAccess.RequireCreatorAsync(users, bloggers, brandFaces, query.TelegramUserId, cancellationToken);
        return await applications.GetForCreatorAsync(creator.Role, creator.ProfileId, query.ApplicationId, cancellationToken);
    }
}

public sealed class GetCampaignApplicationInboxHandler(
    IPlatformUserRepository users,
    IBusinessProfileRepository businesses,
    ICampaignRepository campaigns,
    ICampaignApplicationReadModel applications) : IRequestHandler<GetCampaignApplicationInboxQuery, CampaignApplicationInboxResult>
{
    public async Task<CampaignApplicationInboxResult> Handle(GetCampaignApplicationInboxQuery query, CancellationToken cancellationToken)
    {
        var business = await CampaignManagementAccess.RequireBusinessAsync(users, businesses, query.TelegramUserId, cancellationToken);
        var campaign = await campaigns.GetByIdForBusinessAsync(query.CampaignId, business.Id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign was not found.");
        return await applications.SearchForBusinessAsync(business.Id, campaign.Id, new CampaignApplicationSearch(query.Status, query.Page, query.PageSize), cancellationToken);
    }
}

public sealed class WithdrawMyCampaignApplicationHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    ICampaignApplicationRepository applications,
    IUnitOfWork unitOfWork,
    IBrandFaceProfileRepository? brandFaces = null) : IRequestHandler<WithdrawMyCampaignApplicationCommand, CampaignApplicationDecisionDto>
{
    public async Task<CampaignApplicationDecisionDto> Handle(WithdrawMyCampaignApplicationCommand command, CancellationToken cancellationToken)
    {
        var creator = await CampaignApplicationAccess.RequireCreatorAsync(users, bloggers, brandFaces, command.TelegramUserId, cancellationToken);
        var application = await applications.GetByIdAsync(command.ApplicationId, cancellationToken);
        if (application is null || !creator.Owns(application))
        {
            throw new InvalidOperationException("Campaign application was not found.");
        }

        if (application.Status == CampaignApplicationStatus.Withdrawn)
        {
            return new CampaignApplicationDecisionDto(application.Id, (int)application.Status, null);
        }

        if (!CampaignApplicationLifecycle.IsPending(application.Status))
        {
            throw new InvalidOperationException("Only pending campaign applications can be withdrawn.");
        }

        application.Withdraw();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CampaignApplicationDecisionDto(application.Id, (int)application.Status, null);
    }
}

public sealed class DecideCampaignApplicationHandler(
    IPlatformUserRepository users,
    IBusinessProfileRepository businesses,
    ICampaignRepository campaigns,
    ICampaignApplicationRepository applications,
    IDealRepository deals,
    IUnitOfWork unitOfWork,
    IBloggerProfileRepository bloggers,
    ITelegramBotClient? botClient = null,
    ILogger<DecideCampaignApplicationHandler>? logger = null,
    IBrandFaceProfileRepository? brandFaces = null) : IRequestHandler<DecideCampaignApplicationCommand, CampaignApplicationDecisionDto>
{
    public async Task<CampaignApplicationDecisionDto> Handle(DecideCampaignApplicationCommand command, CancellationToken cancellationToken)
    {
        var business = await DealAccess.RequireBusinessAsync(users, businesses, command.TelegramUserId, cancellationToken);
        var campaign = await campaigns.GetByIdForBusinessAsync(command.CampaignId, business.Id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign was not found.");
        var application = await applications.GetByIdAsync(command.ApplicationId, cancellationToken);
        if (application is null || application.CampaignId != campaign.Id)
        {
            throw new InvalidOperationException("Campaign application was not found.");
        }

        return command.Decision == CampaignApplicationStatus.Accepted
            ? await AcceptAsync(application, business, cancellationToken)
            : await RejectAsync(application, cancellationToken);
    }

    private async Task<CampaignApplicationDecisionDto> AcceptAsync(CampaignApplication application, BusinessProfile business, CancellationToken cancellationToken)
    {
        var existingDeal = await deals.GetByCampaignApplicationIdAsync(application.Id, cancellationToken);
        if (application.Status == CampaignApplicationStatus.Accepted)
        {
            if (existingDeal is null)
            {
                throw new InvalidOperationException("Accepted campaign application has no deal.");
            }

            return new CampaignApplicationDecisionDto(application.Id, (int)application.Status, existingDeal.Id);
        }

        if (!CampaignApplicationLifecycle.IsPending(application.Status))
        {
            throw new InvalidOperationException("Campaign application decision conflicts with its current status.");
        }

        if (existingDeal is not null)
        {
            throw new InvalidOperationException("Campaign application decision conflicts with an existing deal.");
        }

        application.Accept();
        var deal = Deal.Create(application.Id, application.CreatorRole, application.CreatorId, business.Id, CampaignTermsSnapshot.FromCampaign(application.Campaign));
        await deals.AddAsync(deal, cancellationToken);
        if (!await unitOfWork.TrySaveChangesAsync(cancellationToken))
        {
            var persistedDeal = await deals.GetByCampaignApplicationIdAsync(application.Id, cancellationToken);
            if (persistedDeal is not null)
            {
                return new CampaignApplicationDecisionDto(application.Id, (int)CampaignApplicationStatus.Accepted, persistedDeal.Id);
            }

            throw new InvalidOperationException("Campaign application decision conflicts with an existing deal.");
        }

        var creatorChatId = await CampaignApplicationAccess.FindCreatorTelegramUserIdAsync(bloggers, brandFaces, application, cancellationToken);
        if (creatorChatId is not null)
        {
            await BestEffortTelegramNotification.SendAsync(botClient, logger, creatorChatId.Value,
                BotMessages.CampaignApplicationAccepted(application.Campaign.Title), $"/deal/{deal.Id}", cancellationToken);
        }
        return new CampaignApplicationDecisionDto(application.Id, (int)application.Status, deal.Id);
    }

    private async Task<CampaignApplicationDecisionDto> RejectAsync(CampaignApplication application, CancellationToken cancellationToken)
    {
        if (application.Status == CampaignApplicationStatus.Rejected)
        {
            return new CampaignApplicationDecisionDto(application.Id, (int)application.Status, null);
        }

        if (!CampaignApplicationLifecycle.IsPending(application.Status))
        {
            throw new InvalidOperationException("Campaign application decision conflicts with its current status.");
        }

        application.Reject();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CampaignApplicationDecisionDto(application.Id, (int)application.Status, null);
    }
}

// The creator side of the marketplace: a blogger or a brand face, by the selected role only (D46).
internal sealed record CreatorContext(MarketplaceRole Role, Guid ProfileId, long TelegramUserId, string Name)
{
    public bool Owns(CampaignApplication application) => application.CreatorRole == Role && application.CreatorId == ProfileId;
}

public static class CreatorRoles
{
    public const string Blogger = "blogger";
    public const string BrandFace = "brandFace";

    public static string Of(MarketplaceRole role) => role == MarketplaceRole.BrandFace ? BrandFace : Blogger;
}

internal static class CampaignApplicationAccess
{
    // A blogger needs an approved profile; a brand face has no moderation (D46), only an existing profile.
    internal static async Task<CreatorContext> RequireCreatorAsync(
        IPlatformUserRepository users,
        IBloggerProfileRepository bloggers,
        IBrandFaceProfileRepository? brandFaces,
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        var user = await users.GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
        if (user is null || user.IsBlocked || user.IsDeleted)
        {
            throw new UnauthorizedAccessException("An active creator marketplace role is required.");
        }

        if (user.SelectedMarketplaceRole == MarketplaceRole.BrandFace && brandFaces is not null)
        {
            var brandFace = await brandFaces.GetByTelegramUserIdAsync(telegramUserId, cancellationToken)
                ?? throw new UnauthorizedAccessException("A brand face profile is required.");
            return new CreatorContext(MarketplaceRole.BrandFace, brandFace.Id, brandFace.TelegramUserId, brandFace.Name);
        }

        var blogger = await RequireBloggerAsync(users, bloggers, telegramUserId, cancellationToken);
        return new CreatorContext(MarketplaceRole.Blogger, blogger.Id, blogger.TelegramUserId, blogger.Name);
    }

    internal static async Task<long?> FindCreatorTelegramUserIdAsync(
        IBloggerProfileRepository bloggers,
        IBrandFaceProfileRepository? brandFaces,
        CampaignApplication application,
        CancellationToken cancellationToken)
    {
        if (application.BrandFaceId is { } brandFaceId)
        {
            return brandFaces is null ? null : (await brandFaces.GetByIdAsync(brandFaceId, cancellationToken))?.TelegramUserId;
        }

        return application.BloggerId is { } bloggerId ? (await bloggers.GetByIdAsync(bloggerId, cancellationToken))?.TelegramUserId : null;
    }

    internal static async Task<BloggerProfile> RequireBloggerAsync(
        IPlatformUserRepository users,
        IBloggerProfileRepository bloggers,
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        var user = await users.GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
        if (user is null || user.IsBlocked || user.IsDeleted || user.SelectedMarketplaceRole != MarketplaceRole.Blogger)
        {
            throw new UnauthorizedAccessException("An active blogger marketplace role is required.");
        }

        var blogger = await bloggers.GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
        if (blogger is null || blogger.Status != BloggerStatus.Approved)
        {
            throw new UnauthorizedAccessException("An approved blogger profile is required.");
        }

        return blogger;
    }
}

internal static class CampaignApplicationLifecycle
{
    internal static bool IsPending(CampaignApplicationStatus status) =>
        status is CampaignApplicationStatus.Sent or CampaignApplicationStatus.Viewed;
}
