using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Campaigns;

public sealed class CampaignManagementQueryTests
{
    [Fact]
    public void Default_management_query_is_valid()
    {
        var result = new SearchMyCampaignsValidator().Validate(new SearchMyCampaignsQuery(1, null, null, "newest", 1, 20));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(99)]
    public void Invalid_campaign_status_is_rejected(int status)
    {
        var result = new SearchMyCampaignsValidator().Validate(new SearchMyCampaignsQuery(1, status, null, "newest", 1, 20));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(100_001, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public void Invalid_pagination_is_rejected(int page, int pageSize)
    {
        var result = new SearchMyCampaignsValidator().Validate(new SearchMyCampaignsQuery(1, null, null, "newest", page, pageSize));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("promoted")]
    [InlineData("price")]
    public void Invalid_sort_is_rejected(string sort)
    {
        var result = new SearchMyCampaignsValidator().Validate(new SearchMyCampaignsQuery(1, null, null, sort, 1, 20));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Excessive_query_length_is_rejected()
    {
        var result = new SearchMyCampaignsValidator().Validate(new SearchMyCampaignsQuery(1, null, new string('q', 101), "newest", 1, 20));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Active_business_query_is_scoped_to_its_own_business_and_normalizes_query()
    {
        var user = ActiveBusinessUser(101);
        var business = BusinessProfile.Create(user.TelegramUserId, "Business", "tashkent");
        var catalog = new CapturingCatalog();
        var handler = new SearchMyCampaignsHandler(new Users(user), new Businesses(business), catalog);

        await handler.Handle(new SearchMyCampaignsQuery(user.TelegramUserId, (int)CampaignStatus.Draft, "  launch  ", "newest", 2, 10), CancellationToken.None);

        Assert.Equal(business.Id, catalog.BusinessId);
        Assert.Equal("launch", catalog.Search?.Query);
        Assert.Equal((int)CampaignStatus.Draft, catalog.Search?.Status);
        Assert.Equal(2, catalog.Search?.Page);
        Assert.Equal(10, catalog.Search?.PageSize);
    }

    [Theory]
    [InlineData(MarketplaceRole.Blogger)]
    [InlineData(MarketplaceRole.BrandFace)]
    public async Task Non_business_active_role_is_forbidden(MarketplaceRole role)
    {
        var user = PlatformUser.Create(102, "User", null);
        user.SelectMarketplaceRole(role);
        var business = BusinessProfile.Create(user.TelegramUserId, "Business", "tashkent");
        var handler = new SearchMyCampaignsHandler(new Users(user), new Businesses(business), new CapturingCatalog());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new SearchMyCampaignsQuery(user.TelegramUserId, null, null, "newest", 1, 20), CancellationToken.None));
    }

    [Fact]
    public async Task Missing_business_profile_is_forbidden()
    {
        var user = ActiveBusinessUser(103);
        var handler = new SearchMyCampaignsHandler(new Users(user), new Businesses(), new CapturingCatalog());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new SearchMyCampaignsQuery(user.TelegramUserId, null, null, "newest", 1, 20), CancellationToken.None));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Blocked_or_deleted_owner_is_forbidden(bool blocked, bool deleted)
    {
        var user = ActiveBusinessUser(104);
        if (blocked) user.SetBlocked(true);
        if (deleted) user.SoftDelete(user.TelegramUserId);
        var business = BusinessProfile.Create(user.TelegramUserId, "Business", "tashkent");
        var handler = new SearchMyCampaignsHandler(new Users(user), new Businesses(business), new CapturingCatalog());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new SearchMyCampaignsQuery(user.TelegramUserId, null, null, "newest", 1, 20), CancellationToken.None));
    }

    [Fact]
    public async Task Owner_details_return_not_found_when_the_scoped_catalog_has_no_campaign()
    {
        var user = ActiveBusinessUser(105);
        var business = BusinessProfile.Create(user.TelegramUserId, "Business", "tashkent");
        var catalog = new CapturingCatalog();
        var handler = new GetMyCampaignHandler(new Users(user), new Businesses(business), catalog);

        var result = await handler.Handle(new GetMyCampaignQuery(user.TelegramUserId, Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(business.Id, catalog.DetailsBusinessId);
    }

    [Fact]
    public void Management_item_contract_contains_only_management_fields()
    {
        var properties = typeof(MyCampaignItemDto).GetProperties().Select(property => property.Name).OrderBy(name => name).ToArray();

        Assert.Equal(["ApplicationsCount", "Categories", "City", "CreatedAtUtc", "Deadline", "Id", "IsPromoted", "MaxBudget", "MinBudget", "Status", "Title", "UpdatedAtUtc"], properties);
    }

    private static PlatformUser ActiveBusinessUser(long telegramUserId)
    {
        var user = PlatformUser.Create(telegramUserId, "Business", null);
        user.SelectMarketplaceRole(MarketplaceRole.Business);
        return user;
    }

    private sealed class Users(params PlatformUser[] initial) : IPlatformUserRepository
    {
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) =>
            Task.FromResult(initial.SingleOrDefault(user => user.TelegramUserId == telegramUserId));

        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>([]);
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public Task AddAsync(PlatformUser user, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class Businesses(params BusinessProfile[] initial) : IBusinessProfileRepository
    {
        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(initial.SingleOrDefault(business => business.Id == id));
        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(initial.SingleOrDefault(business => business.TelegramUserId == telegramUserId));
        public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CapturingCatalog : ICampaignManagementReadModel
    {
        public Guid? BusinessId { get; private set; }
        public MyCampaignsSearch? Search { get; private set; }
        public Guid? DetailsBusinessId { get; private set; }

        public Task<MyCampaignsResult> SearchAsync(Guid businessId, MyCampaignsSearch search, CancellationToken cancellationToken)
        {
            BusinessId = businessId;
            Search = search;
            return Task.FromResult(new MyCampaignsResult([], 0, search.Page, search.PageSize, false));
        }

        public Task<MyCampaignDetailsDto?> GetByIdAsync(Guid businessId, Guid campaignId, CancellationToken cancellationToken)
        {
            DetailsBusinessId = businessId;
            return Task.FromResult<MyCampaignDetailsDto?>(null);
        }
    }
}
