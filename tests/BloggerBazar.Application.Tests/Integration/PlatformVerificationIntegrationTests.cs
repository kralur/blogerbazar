using System.Net.Http.Json;
using System.Text.Json;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using BloggerBazar.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BloggerBazar.Application.Tests.Integration;

// D67: a verified channel is stored with its source and date and shown to companies on the blogger's page.
[Collection(ApiHostCollection.Name)]
public sealed class PlatformVerificationIntegrationTests(BloggerBazarApiFactory factory) : IClassFixture<BloggerBazarApiFactory>
{
    [IntegrationFact]
    public async Task A_verified_channel_is_stored_and_shown_on_the_blogger_page()
    {
        const long company = 1_700_001;
        const long creator = 1_700_002;
        Guid bloggerId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
            var businessUser = PlatformUser.Create(company, "Viewer", null);
            businessUser.SelectMarketplaceRole(MarketplaceRole.Business);
            businessUser.VerifyPhone("+998901250001", DateTime.UtcNow);
            var creatorUser = PlatformUser.Create(creator, "Channel", null);
            creatorUser.SelectMarketplaceRole(MarketplaceRole.Blogger);
            var blogger = BloggerProfile.Create(creator, "Channel Owner", "tashkent", ["food"]);
            blogger.Approve(verified: false);
            var telegram = SocialPlatform.Create(blogger.Id, "telegram", "https://t.me/channel_owner", 1, null);
            telegram.MarkVerified("telegram", 12_345, new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc));
            dbContext.AddRange(businessUser, creatorUser, blogger, telegram);
            await dbContext.SaveChangesAsync();
            bloggerId = blogger.Id;
        }

        using var app = factory.CreateClientFor(company);
        var page = await app.GetFromJsonAsync<JsonElement>($"/api/bloggers/{bloggerId}");
        var platform = page.GetProperty("platforms").EnumerateArray().Single();
        Assert.Equal("telegram", platform.GetProperty("verificationSource").GetString());
        Assert.Equal(12_345, platform.GetProperty("followers").GetInt32());
        Assert.Equal(new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc), platform.GetProperty("verifiedAtUtc").GetDateTime().ToUniversalTime());
    }
}
