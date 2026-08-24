using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.BrandFaces;
using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Tests.Features.BrandFaces;

public sealed class BrandFaceCatalogQueryTests
{
    [Fact]
    public void Defaults_are_valid()
    {
        var result = new SearchBrandFaceCatalogValidator().Validate(new SearchBrandFaceCatalogQuery(null, null, null, null, null, null));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public void Invalid_page_values_are_rejected(int page, int pageSize)
    {
        var result = new SearchBrandFaceCatalogValidator().Validate(new SearchBrandFaceCatalogQuery(null, null, null, null, null, null, Page: page, PageSize: pageSize));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("rating")]
    [InlineData("")]
    [InlineData("popular")]
    public void Invalid_sort_is_rejected(string sort)
    {
        var result = new SearchBrandFaceCatalogValidator().Validate(new SearchBrandFaceCatalogQuery(null, null, null, null, null, null, sort));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Invalid_string_lengths_and_price_ranges_are_rejected()
    {
        var validator = new SearchBrandFaceCatalogValidator();
        var overlong = new string('a', 101);

        var invalidText = validator.Validate(new SearchBrandFaceCatalogQuery(overlong, new string('c', 81), new string('g', 51), new string('l', 33), null, null));
        var negativePrice = validator.Validate(new SearchBrandFaceCatalogQuery(null, null, null, null, -1, null));
        var reversedRange = validator.Validate(new SearchBrandFaceCatalogQuery(null, null, null, null, 200_000, 100_000));

        Assert.False(invalidText.IsValid);
        Assert.False(negativePrice.IsValid);
        Assert.False(reversedRange.IsValid);
    }

    [Fact]
    public async Task Normalized_equivalent_requests_share_one_cache_key()
    {
        var catalog = new CapturingCatalogReadModel();
        var cache = new RecordingCache();
        var handler = new SearchBrandFaceCatalogHandler(catalog, cache);

        await handler.Handle(new SearchBrandFaceCatalogQuery("  Madina  ", " Tashkent ", " Beauty ", " UZ ", 100, 200, "PROMOTED", 1, 20), CancellationToken.None);
        await handler.Handle(new SearchBrandFaceCatalogQuery("madina", "tashkent", "beauty", "uz", 100, 200, "promoted", 1, 20), CancellationToken.None);

        Assert.Equal(2, cache.Keys.Count);
        Assert.Equal(cache.Keys[0], cache.Keys[1]);
        Assert.Equal("madina", catalog.Searches[0].Query);
        Assert.Equal("tashkent", catalog.Searches[0].City);
        Assert.Equal("beauty", catalog.Searches[0].Category);
        Assert.Equal("uz", catalog.Searches[0].Language);
    }

    [Fact]
    public async Task Filters_sort_and_page_produce_distinct_cache_keys()
    {
        var cache = new RecordingCache();
        var handler = new SearchBrandFaceCatalogHandler(new CapturingCatalogReadModel(), cache);

        await handler.Handle(new SearchBrandFaceCatalogQuery(null, null, null, null, null, null, "promoted", 1, 20), CancellationToken.None);
        await handler.Handle(new SearchBrandFaceCatalogQuery(null, null, null, null, null, null, "newest", 1, 20), CancellationToken.None);
        await handler.Handle(new SearchBrandFaceCatalogQuery(null, null, null, "uz", null, null, "promoted", 2, 20), CancellationToken.None);

        Assert.Equal(3, cache.Keys.Distinct().Count());
    }

    [Fact]
    public async Task Read_model_failures_are_not_cached()
    {
        var cache = new RecordingCache();
        var handler = new SearchBrandFaceCatalogHandler(new ThrowingCatalogReadModel(), cache);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new SearchBrandFaceCatalogQuery(null, null, null, null, null, null), CancellationToken.None));

        Assert.Empty(cache.Values);
    }

    [Fact]
    public void Catalog_item_does_not_expose_contacts_or_internal_fields()
    {
        var properties = typeof(BrandFaceCatalogItemDto).GetProperties().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(["AvatarUrl", "Categories", "City", "CollaborationPrice", "CreatedAtUtc", "Id", "IsPromoted", "Languages", "Name"], properties.OrderBy(name => name));
        Assert.DoesNotContain("Telegram", properties);
        Assert.DoesNotContain("Instagram", properties);
        Assert.DoesNotContain("PortfolioUrl", properties);
        Assert.DoesNotContain("Description", properties);
        Assert.DoesNotContain("TelegramUserId", properties);
    }

    [Fact]
    public async Task Brand_face_upsert_rotates_the_catalog_namespace()
    {
        var cache = new RecordingCache();
        var handler = new UpsertBrandFaceProfileHandler(new InMemoryBrandFaceRepository(), new NoOpUnitOfWork(), cache);

        await handler.Handle(new UpsertBrandFaceProfileCommand(9001, "Madina", "tashkent", null, null, ["uz"], ["beauty"], null, null, "@madina", null, 300_000, null, null), CancellationToken.None);

        Assert.Equal(1, cache.Rotations);
    }

    private sealed class CapturingCatalogReadModel : IBrandFaceCatalogReadModel
    {
        public List<BrandFaceCatalogSearch> Searches { get; } = [];

        public Task<BrandFaceCatalogResult> SearchAsync(BrandFaceCatalogSearch search, CancellationToken cancellationToken)
        {
            Searches.Add(search);
            return Task.FromResult(new BrandFaceCatalogResult([], 0, search.Page, search.PageSize, false));
        }
    }

    private sealed class ThrowingCatalogReadModel : IBrandFaceCatalogReadModel
    {
        public Task<BrandFaceCatalogResult> SearchAsync(BrandFaceCatalogSearch search, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Database unavailable.");
    }

    private sealed class RecordingCache : ICatalogCache
    {
        public List<string> Keys { get; } = [];
        public List<object> Values { get; } = [];
        public int Rotations { get; private set; }

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

        public Task<string> GetNamespaceVersionAsync(CancellationToken cancellationToken) => Task.FromResult("catalog-version");
        public Task RotateNamespaceVersionAsync(CancellationToken cancellationToken) { Rotations++; return Task.CompletedTask; }
    }

    private sealed class InMemoryBrandFaceRepository : IBrandFaceProfileRepository
    {
        private BrandFaceProfile? profile;

        public Task<BrandFaceProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(profile?.Id == id ? profile : null);
        public Task<BrandFaceProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(profile?.TelegramUserId == telegramUserId ? profile : null);
        public Task<BrandFaceProfile?> GetIncludingDeletedByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
        public Task<IReadOnlyList<BrandFaceProfile>> GetAllAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BrandFaceProfile>>([]);
        public Task<IReadOnlyList<BrandFaceProfile>> SearchAsync(string? query, string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BrandFaceProfile>>([]);
        public Task AddAsync(BrandFaceProfile value, CancellationToken cancellationToken) { profile = value; return Task.CompletedTask; }
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
