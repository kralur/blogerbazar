using System.Reflection;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Deals;

internal static class DealTestData
{
    public static PlatformUser User(long telegramUserId, MarketplaceRole? role)
    {
        var user = PlatformUser.Create(telegramUserId, "User", null);
        if (role is not null)
        {
            user.SelectMarketplaceRole(role.Value);
        }

        return user;
    }

    public static BloggerProfile Blogger(long telegramUserId, string name = "Blogger") => BloggerProfile.Create(telegramUserId, name, "tashkent", ["beauty"]);

    public static BusinessProfile Business(long telegramUserId, string name = "Business") => BusinessProfile.Create(telegramUserId, name, "tashkent");

    public static Deal CampaignDeal(BloggerProfile blogger, BusinessProfile business)
    {
        var campaign = Campaign.Create(business.Id, "Campaign", "Description", ["beauty"], ["Reel"], 100, 200, "tashkent", null);
        return Attach(Deal.Create(Guid.NewGuid(), blogger.Id, business.Id, CampaignTermsSnapshot.FromCampaign(campaign)), blogger, business);
    }

    public static Deal Attach(Deal deal, BloggerProfile blogger, BusinessProfile business)
    {
        Set(deal, nameof(Deal.Blogger), blogger);
        Set(deal, nameof(Deal.Business), business);
        return deal;
    }

    public static void Set<T>(object target, string propertyName, T value) =>
        target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!.SetValue(target, value);
}

internal sealed class FakeUsers(params PlatformUser[] users) : IPlatformUserRepository
{
    public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(users.SingleOrDefault(user => user.TelegramUserId == telegramUserId));
    public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>([]);
    public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(0);
    public Task AddAsync(PlatformUser user, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class FakeBloggers(params BloggerProfile[] bloggers) : IBloggerProfileRepository
{
    public Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(bloggers.SingleOrDefault(blogger => blogger.Id == id));
    public Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(bloggers.SingleOrDefault(blogger => blogger.TelegramUserId == telegramUserId));
    public Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
    public Task AddAsync(BloggerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class FakeBusinesses(params BusinessProfile[] businesses) : IBusinessProfileRepository
{
    public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(businesses.SingleOrDefault(business => business.Id == id));
    public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(businesses.SingleOrDefault(business => business.TelegramUserId == telegramUserId));
    public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
}

// Mirrors the scoped repository contract: lookups only succeed for the participant of the selected role.
internal sealed class FakeDeals(params Deal[] deals) : IDealRepository
{
    public int CompletionAttempts { get; private set; }
    public Action<Deal>? BeforeCompletion { get; init; }

    public Task<Deal?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(deals.SingleOrDefault(deal => deal.Id == id));

    public Task<Deal?> GetForParticipantAsync(Guid dealId, MarketplaceRole role, Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult(deals.SingleOrDefault(deal => deal.Id == dealId && role switch
        {
            MarketplaceRole.Blogger => deal.BloggerId == profileId,
            MarketplaceRole.Business => deal.BusinessId == profileId,
            _ => false
        }));

    public Task<bool> TryCompleteAsync(Guid dealId, DateTime completedAtUtc, CancellationToken cancellationToken)
    {
        CompletionAttempts++;
        var deal = deals.Single(candidate => candidate.Id == dealId);
        BeforeCompletion?.Invoke(deal);
        if (deal.Status != DealStatus.Active)
        {
            return Task.FromResult(false);
        }

        deal.Complete();
        return Task.FromResult(true);
    }

    public List<Deal> Added { get; } = [];
    public Task<bool> ExistsForApplicationAsync(Guid campaignApplicationId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<Deal?> GetByCollaborationRequestIdAsync(Guid collaborationRequestId, CancellationToken cancellationToken) =>
        Task.FromResult(Added.SingleOrDefault(deal => deal.CollaborationRequestId == collaborationRequestId));
    public Task AddAsync(Deal deal, CancellationToken cancellationToken) { Added.Add(deal); return Task.CompletedTask; }
}

internal sealed class FakeDealReadModel(params DealReadRow[] rows) : IDealReadModel
{
    public List<(MarketplaceRole Role, Guid ProfileId)> Queries { get; } = [];

    public Task<IReadOnlyList<DealReadRow>> ListForParticipantAsync(MarketplaceRole role, Guid profileId, CancellationToken cancellationToken)
    {
        Queries.Add((role, profileId));
        return Task.FromResult<IReadOnlyList<DealReadRow>>(rows.ToArray());
    }

    public Task<DealReadRow?> FindForParticipantAsync(Guid dealId, MarketplaceRole role, Guid profileId, CancellationToken cancellationToken)
    {
        Queries.Add((role, profileId));
        return Task.FromResult(rows.SingleOrDefault(row => row.Id == dealId));
    }
}

internal sealed class SpyBotClient : ITelegramBotClient
{
    public List<long> NotifiedChats { get; } = [];
    public List<string> Texts { get; } = [];
    public List<string?> Routes { get; } = [];
    public List<string?> Buttons { get; } = [];
    public Task SendStartMessageAsync(long chatId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SendNotificationAsync(long chatId, string text, CancellationToken cancellationToken) => Record(chatId, text, null);

    public Task SendNotificationAsync(long chatId, string text, string miniAppRoute, CancellationToken cancellationToken) => Record(chatId, text, miniAppRoute);

    public Task SendNotificationAsync(long chatId, BotText text, string? miniAppRoute, CancellationToken cancellationToken)
    {
        Buttons.Add(text.Button?.Russian);
        return Record(chatId, text.For(null), miniAppRoute);
    }

    private Task Record(long chatId, string text, string? route)
    {
        NotifiedChats.Add(chatId);
        Texts.Add(text);
        Routes.Add(route);
        return Task.CompletedTask;
    }
}

internal sealed class SpyUnitOfWork(bool uniqueConflict = false) : IUnitOfWork
{
    public int SaveCalls { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCalls++;
        return Task.FromResult(1);
    }

    public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCalls++;
        return Task.FromResult(!uniqueConflict);
    }
}
