using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Features.Deals;

internal sealed record DealParticipantContext(MarketplaceRole Role, Guid ProfileId)
{
    // The creator side of a deal: a blogger or a brand face (D46).
    public bool IsCreator => Role is MarketplaceRole.Blogger or MarketplaceRole.BrandFace;
}

internal static class DealAccess
{
    internal static async Task<BusinessProfile> RequireBusinessAsync(
        IPlatformUserRepository users,
        IBusinessProfileRepository businesses,
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        var user = await RequireActiveUserAsync(users, telegramUserId, cancellationToken);
        if (user.SelectedMarketplaceRole != MarketplaceRole.Business)
        {
            throw new UnauthorizedAccessException("An active business marketplace role is required.");
        }

        return await businesses.GetByTelegramUserIdAsync(telegramUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("An active business profile is required.");
    }

    internal static async Task<DealParticipantContext> RequireCollaborationParticipantAsync(
        IPlatformUserRepository users,
        IBloggerProfileRepository bloggers,
        IBusinessProfileRepository businesses,
        CollaborationRequest request,
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        var user = await RequireActiveUserAsync(users, telegramUserId, cancellationToken);
        return user.SelectedMarketplaceRole switch
        {
            MarketplaceRole.Blogger => await RequireBloggerParticipantAsync(bloggers, request, telegramUserId, cancellationToken),
            MarketplaceRole.Business => await RequireBusinessParticipantAsync(businesses, request, telegramUserId, cancellationToken),
            _ => throw new UnauthorizedAccessException("The selected marketplace role cannot create a deal.")
        };
    }

    // Null means the account has no deal side for its selected role; callers answer [] or 404.
    internal static async Task<DealParticipantContext?> FindDealParticipantAsync(
        IPlatformUserRepository users,
        IBloggerProfileRepository bloggers,
        IBusinessProfileRepository businesses,
        long telegramUserId,
        CancellationToken cancellationToken,
        IBrandFaceProfileRepository? brandFaces = null)
    {
        var user = await users.GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
        if (user is null || user.IsBlocked || user.IsDeleted)
        {
            return null;
        }

        return user.SelectedMarketplaceRole switch
        {
            MarketplaceRole.Blogger => await bloggers.GetByTelegramUserIdAsync(telegramUserId, cancellationToken) is { } blogger
                ? new DealParticipantContext(MarketplaceRole.Blogger, blogger.Id)
                : null,
            MarketplaceRole.Business => await businesses.GetByTelegramUserIdAsync(telegramUserId, cancellationToken) is { } business
                ? new DealParticipantContext(MarketplaceRole.Business, business.Id)
                : null,
            // D46: a brand face is a deal's creator side exactly like a blogger.
            MarketplaceRole.BrandFace => brandFaces is not null && await brandFaces.GetByTelegramUserIdAsync(telegramUserId, cancellationToken) is { } brandFace
                ? new DealParticipantContext(MarketplaceRole.BrandFace, brandFace.Id)
                : null,
            _ => null
        };
    }

    internal static InvalidOperationException DealNotFound() => new("Deal was not found.");

    private static async Task<PlatformUser> RequireActiveUserAsync(
        IPlatformUserRepository users,
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        var user = await users.GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
        if (user is null || user.IsBlocked || user.IsDeleted || user.SelectedMarketplaceRole is null)
        {
            throw new UnauthorizedAccessException("An active marketplace account is required.");
        }

        return user;
    }

    private static async Task<DealParticipantContext> RequireBloggerParticipantAsync(
        IBloggerProfileRepository bloggers,
        CollaborationRequest request,
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        var blogger = await bloggers.GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
        if (blogger is null || blogger.Id != request.BloggerId)
        {
            throw new UnauthorizedAccessException("You are not the blogger participant in this request.");
        }

        return new DealParticipantContext(MarketplaceRole.Blogger, blogger.Id);
    }

    private static async Task<DealParticipantContext> RequireBusinessParticipantAsync(
        IBusinessProfileRepository businesses,
        CollaborationRequest request,
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        var business = await businesses.GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
        if (business is null || business.Id != request.BusinessId)
        {
            throw new UnauthorizedAccessException("You are not the business participant in this request.");
        }

        return new DealParticipantContext(MarketplaceRole.Business, business.Id);
    }
}
