using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using BloggerBazar.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BloggerBazar.Application.Tests.Integration;

// A saved blogger carries the facts the catalog card shows (reach, ER, prices, platform), so Favorites looks like Search.
[Collection(ApiHostCollection.Name)]
public sealed class FavoritesIntegrationTests(BloggerBazarApiFactory factory) : IClassFixture<BloggerBazarApiFactory>
{
    [IntegrationFact]
    public async Task A_saved_blogger_comes_with_the_catalog_card_facts()
    {
        const long company = 1_600_001;
        const long creator = 1_600_002;
        Guid bloggerId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
            var businessUser = PlatformUser.Create(company, "Saver", null);
            businessUser.SelectMarketplaceRole(MarketplaceRole.Business);
            businessUser.VerifyPhone("+998901240001", DateTime.UtcNow);
            var creatorUser = PlatformUser.Create(creator, "Saved", null);
            creatorUser.SelectMarketplaceRole(MarketplaceRole.Blogger);
            var blogger = BloggerProfile.Create(creator, "Saved Blogger", "tashkent", ["food"]);
            blogger.UpdatePublicProfile("Saved Blogger", null, null, "tashkent", ["food"], null, null, null, null,
                120_000, 40_000, 4.5m, 300_000, 500_000, null, null, false);
            blogger.Approve(verified: false);
            dbContext.AddRange(businessUser, creatorUser, blogger);
            dbContext.Add(SocialPlatform.Create(blogger.Id, "instagram", "https://instagram.com/saved_blogger", 120_000, null));
            await dbContext.SaveChangesAsync();
            bloggerId = blogger.Id;
        }

        using var app = factory.CreateClientFor(company);
        Assert.Equal(HttpStatusCode.OK, (await app.PostAsync($"/api/favorites/{bloggerId}", null)).StatusCode);

        var page = await app.GetFromJsonAsync<JsonElement>("/api/favorites?page=1&pageSize=10");
        var item = page.GetProperty("items").EnumerateArray().Single(entry => entry.GetProperty("bloggerId").GetGuid() == bloggerId);
        Assert.Equal(40_000, item.GetProperty("averageReach").GetInt32());
        Assert.Equal(4.5m, item.GetProperty("engagementRate").GetDecimal());
        Assert.Equal(300_000, item.GetProperty("storiesPrice").GetInt32());
        Assert.Equal(500_000, item.GetProperty("reelsPrice").GetInt32());
        Assert.Equal("instagram", item.GetProperty("platform").GetString());
    }
}
