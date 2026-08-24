using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Campaigns;

namespace BloggerBazar.Application.Tests.Features.Campaigns;

public sealed class CampaignCatalogQueryTests
{
    [Fact]
    public void Defaults_are_valid()
    {
        var result = new SearchCampaignCatalogValidator().Validate(new SearchCampaignCatalogQuery(null, null, null, null, null, null, null));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(100_001, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public void Invalid_page_values_are_rejected(int page, int pageSize)
    {
        var result = new SearchCampaignCatalogValidator().Validate(new SearchCampaignCatalogQuery(null, null, null, null, null, null, null, Page: page, PageSize: pageSize));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("popular")]
    [InlineData("")]
    [InlineData("rating")]
    public void Invalid_sort_is_rejected(string sort)
    {
        var result = new SearchCampaignCatalogValidator().Validate(new SearchCampaignCatalogQuery(null, null, null, null, null, null, null, sort));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Invalid_lengths_and_ranges_are_rejected()
    {
        var validator = new SearchCampaignCatalogValidator();
        var deadline = DateTime.UnixEpoch;

        Assert.False(validator.Validate(new SearchCampaignCatalogQuery(new string('q', 101), new string('c', 81), new string('g', 51), null, null, null, null)).IsValid);
        Assert.False(validator.Validate(new SearchCampaignCatalogQuery(null, null, null, -1, null, null, null)).IsValid);
        Assert.False(validator.Validate(new SearchCampaignCatalogQuery(null, null, null, 200_000, 100_000, null, null)).IsValid);
        Assert.False(validator.Validate(new SearchCampaignCatalogQuery(null, null, null, null, null, deadline.AddDays(1), deadline)).IsValid);
    }

    [Fact]
    public async Task Equivalent_normalized_requests_share_a_campaign_namespace_key()
    {
        var catalog = new CapturingReadModel();
        var cache = new RecordingCache();
        var handler = new SearchCampaignCatalogHandler(catalog, cache);
        var deadline = new DateTime(2026, 8, 24, 0, 0, 0, DateTimeKind.Utc);

        await handler.Handle(new SearchCampaignCatalogQuery("  Coffee  ", " Tashkent ", " Other:Custom ", 100, 300, deadline, deadline.AddDays(2), "PROMOTED", 1, 20), CancellationToken.None);
        await handler.Handle(new SearchCampaignCatalogQuery("coffee", "tashkent", "other:custom", 100, 300, deadline, deadline.AddDays(2), "promoted", 1, 20), CancellationToken.None);

        Assert.All(cache.Namespaces, catalogNamespace => Assert.Equal("campaigns", catalogNamespace));
        Assert.Equal(cache.Keys[0], cache.Keys[1]);
        Assert.Equal("coffee", catalog.Searches[0].Query);
        Assert.Equal("tashkent", catalog.Searches[0].City);
        Assert.Equal("other:custom", catalog.Searches[0].Category);
    }

    [Fact]
    public async Task Filters_sort_and_page_produce_distinct_cache_keys()
    {
        var cache = new RecordingCache();
        var handler = new SearchCampaignCatalogHandler(new CapturingReadModel(), cache);

        await handler.Handle(new SearchCampaignCatalogQuery(null, null, null, null, null, null, null, "promoted", 1, 20), CancellationToken.None);
        await handler.Handle(new SearchCampaignCatalogQuery(null, null, null, null, null, null, null, "newest", 1, 20), CancellationToken.None);
        await handler.Handle(new SearchCampaignCatalogQuery(null, "tashkent", null, 10, 20, null, null, "promoted", 2, 20), CancellationToken.None);

        Assert.Equal(3, cache.Keys.Distinct().Count());
    }

    [Fact]
    public async Task Read_model_failure_is_not_cached()
    {
        var cache = new RecordingCache();
        var handler = new SearchCampaignCatalogHandler(new ThrowingReadModel(), cache);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new SearchCampaignCatalogQuery(null, null, null, null, null, null, null), CancellationToken.None));

        Assert.Empty(cache.Values);
    }

    [Fact]
    public void Catalog_item_exposes_only_public_campaign_fields()
    {
        var properties = typeof(CampaignCatalogItemDto).GetProperties().Select(property => property.Name).OrderBy(name => name).ToArray();

        Assert.Equal(["BusinessAvatarUrl", "BusinessName", "Categories", "City", "CreatedAtUtc", "Deadline", "Id", "IsPromoted", "MaxBudget", "MinBudget", "Requirements", "Status", "Title"], properties);
        Assert.DoesNotContain("Phone", properties);
        Assert.DoesNotContain("Email", properties);
        Assert.DoesNotContain("Telegram", properties);
        Assert.DoesNotContain("ApplicationsCount", properties);
        Assert.DoesNotContain("DealId", properties);
    }

    private sealed class CapturingReadModel : ICampaignCatalogReadModel
    {
        public List<CampaignCatalogSearch> Searches { get; } = [];

        public Task<CampaignCatalogResult> SearchAsync(CampaignCatalogSearch search, CancellationToken cancellationToken)
        {
            Searches.Add(search);
            return Task.FromResult(new CampaignCatalogResult([], 0, search.Page, search.PageSize, false));
        }
    }

    private sealed class ThrowingReadModel : ICampaignCatalogReadModel
    {
        public Task<CampaignCatalogResult> SearchAsync(CampaignCatalogSearch search, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Database unavailable.");
    }

    private sealed class RecordingCache : ICatalogCache
    {
        public List<string> Namespaces { get; } = [];
        public List<string> Keys { get; } = [];
        public List<object> Values { get; } = [];

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class
        {
            Keys.Add(key);
            return Task.FromResult<T?>(null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken) where T : class
        {
            Values.Add(value);
            return Task.CompletedTask;
        }

        public Task<string> GetNamespaceVersionAsync(string catalog, CancellationToken cancellationToken)
        {
            Namespaces.Add(catalog);
            return Task.FromResult("catalog-version");
        }
    }
}
