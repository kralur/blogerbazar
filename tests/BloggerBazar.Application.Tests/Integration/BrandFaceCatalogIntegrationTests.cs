using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BloggerBazar.Application.Tests.Integration;

public sealed class BrandFaceCatalogIntegrationTests(BloggerBazarApiFactory factory) : IClassFixture<BloggerBazarApiFactory>
{
    [IntegrationFact]
    public async Task Catalog_projects_public_fields_and_applies_filters_and_pagination()
    {
        await AddProfileAsync(2_100_001, "Madina Beauty", "tashkent", ["beauty"], ["uz"], 150_000);
        await AddProfileAsync(2_100_002, "Madina Fashion", "tashkent", ["fashion"], ["ru"], 250_000);
        await AddProfileAsync(2_100_003, "Aziza Beauty", "samarkand", ["beauty"], ["uz"], null);
        using var client = factory.CreateClient();

        var filtered = await client.GetAsync("/api/brand-faces/catalog?query=madina&city=tashkent&minPrice=100000&maxPrice=200000&page=1&pageSize=1");
        var response = await filtered.Content.ReadFromJsonAsync<JsonElement>();
        var item = response.GetProperty("items").EnumerateArray().Single();

        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        Assert.Equal(1, response.GetProperty("total").GetInt32());
        Assert.Equal(1, response.GetProperty("page").GetInt32());
        Assert.Equal(1, response.GetProperty("pageSize").GetInt32());
        Assert.False(response.GetProperty("hasMore").GetBoolean());
        Assert.Equal("Madina Beauty", item.GetProperty("name").GetString());
        Assert.False(item.TryGetProperty("telegram", out _));
        Assert.False(item.TryGetProperty("instagram", out _));
        Assert.False(item.TryGetProperty("description", out _));
        Assert.False(item.TryGetProperty("telegramUserId", out _));

        var category = await client.GetFromJsonAsync<JsonElement>("/api/brand-faces/catalog?category=beauty");
        var language = await client.GetFromJsonAsync<JsonElement>("/api/brand-faces/catalog?language=ru");
        var nullPriceIncluded = await client.GetFromJsonAsync<JsonElement>("/api/brand-faces/catalog?city=samarkand");
        var nullPriceExcluded = await client.GetFromJsonAsync<JsonElement>("/api/brand-faces/catalog?city=samarkand&minPrice=0");

        Assert.Contains(category.GetProperty("items").EnumerateArray(), candidate => candidate.GetProperty("name").GetString() == "Madina Beauty");
        Assert.Contains(language.GetProperty("items").EnumerateArray(), candidate => candidate.GetProperty("name").GetString() == "Madina Fashion");
        Assert.Single(nullPriceIncluded.GetProperty("items").EnumerateArray());
        Assert.Empty(nullPriceExcluded.GetProperty("items").EnumerateArray());
    }

    [IntegrationFact]
    public async Task Catalog_sorts_prices_with_nulls_last_and_stable_pagination()
    {
        await AddProfileAsync(2_100_011, "Price A", "bukhara", ["beauty"], ["uz"], 200_000);
        await AddProfileAsync(2_100_012, "Price B", "bukhara", ["beauty"], ["uz"], 100_000);
        await AddProfileAsync(2_100_013, "Price Unknown", "bukhara", ["beauty"], ["uz"], null);
        using var client = factory.CreateClient();

        var ascending = await client.GetFromJsonAsync<JsonElement>("/api/brand-faces/catalog?city=bukhara&sort=price_asc&pageSize=20");
        var descending = await client.GetFromJsonAsync<JsonElement>("/api/brand-faces/catalog?city=bukhara&sort=price_desc&pageSize=20");
        var firstPage = await client.GetFromJsonAsync<JsonElement>("/api/brand-faces/catalog?city=bukhara&sort=price_asc&page=1&pageSize=2");
        var secondPage = await client.GetFromJsonAsync<JsonElement>("/api/brand-faces/catalog?city=bukhara&sort=price_asc&page=2&pageSize=2");

        Assert.Equal("Price B", ascending.GetProperty("items")[0].GetProperty("name").GetString());
        Assert.Equal("Price Unknown", ascending.GetProperty("items")[ascending.GetProperty("items").GetArrayLength() - 1].GetProperty("name").GetString());
        Assert.Equal("Price A", descending.GetProperty("items")[0].GetProperty("name").GetString());
        Assert.Equal("Price Unknown", descending.GetProperty("items")[descending.GetProperty("items").GetArrayLength() - 1].GetProperty("name").GetString());
        Assert.True(firstPage.GetProperty("hasMore").GetBoolean());
        Assert.False(secondPage.GetProperty("hasMore").GetBoolean());
        Assert.Empty(firstPage.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()).Intersect(secondPage.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString())));
    }

    [IntegrationFact]
    public async Task Deleted_or_blocked_brand_faces_are_hidden_from_catalog_and_details()
    {
        var visible = await AddProfileAsync(2_100_021, "Visible", "navoi", ["beauty"], ["uz"], 100_000);
        var deleted = await AddProfileAsync(2_100_022, "Deleted", "navoi", ["beauty"], ["uz"], 100_000, profile => profile.SoftDelete());
        var blocked = await AddProfileAsync(2_100_023, "Blocked", "navoi", ["beauty"], ["uz"], 100_000, null, user => user.SetBlocked(true));
        using var client = factory.CreateClient();

        var catalog = await client.GetFromJsonAsync<JsonElement>("/api/brand-faces/catalog?city=navoi");
        var ids = catalog.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()).ToArray();
        var deletedDetails = await client.GetAsync($"/api/brand-faces/{deleted.Id}");
        var blockedDetails = await client.GetAsync($"/api/brand-faces/{blocked.Id}");
        var visibleDetails = await client.GetAsync($"/api/brand-faces/{visible.Id}");

        Assert.Contains(visible.Id.ToString(), ids);
        Assert.DoesNotContain(deleted.Id.ToString(), ids);
        Assert.DoesNotContain(blocked.Id.ToString(), ids);
        Assert.Equal(HttpStatusCode.NotFound, deletedDetails.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, blockedDetails.StatusCode);
        Assert.Equal(HttpStatusCode.OK, visibleDetails.StatusCode);
    }

    [IntegrationFact]
    public async Task Catalog_validation_rejects_invalid_values()
    {
        using var client = factory.CreateClient();

        foreach (var url in new[]
        {
            "/api/brand-faces/catalog?page=0",
            "/api/brand-faces/catalog?pageSize=51",
            "/api/brand-faces/catalog?sort=rating",
            "/api/brand-faces/catalog?minPrice=-1",
            "/api/brand-faces/catalog?minPrice=10&maxPrice=5"
        })
        {
            var response = await client.GetAsync(url);
            Assert.Equal((HttpStatusCode)422, response.StatusCode);
        }
    }

    private async Task<BrandFaceProfile> AddProfileAsync(
        long telegramUserId,
        string name,
        string city,
        string[] categories,
        string[] languages,
        int? price,
        Action<BrandFaceProfile>? profileMutation = null,
        Action<PlatformUser>? userMutation = null)
    {
        var profile = BrandFaceProfile.Create(telegramUserId, name, city, categories);
        profile.Update(name, city, null, null, languages, categories, "Experience", "@instagram", $"@face{telegramUserId}", "https://example.com/portfolio", price, "Description", null);
        profileMutation?.Invoke(profile);
        var user = PlatformUser.Create(telegramUserId, name, $"face{telegramUserId}");
        userMutation?.Invoke(user);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        dbContext.AddRange(profile, user);
        await dbContext.SaveChangesAsync();
        return profile;
    }
}
