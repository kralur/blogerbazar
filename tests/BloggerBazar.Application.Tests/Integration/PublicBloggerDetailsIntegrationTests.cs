using System.Net;
using BloggerBazar.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using BloggerBazar.Infrastructure.Persistence;

namespace BloggerBazar.Application.Tests.Integration;

[Collection(ApiHostCollection.Name)]
public sealed class PublicBloggerDetailsIntegrationTests(BloggerBazarApiFactory factory) : IClassFixture<BloggerBazarApiFactory>
{
    [IntegrationFact]
    public async Task Approved_blogger_details_are_available_to_a_signed_in_user()
    {
        var profile = await AddProfileAsync(1_010_001, profile => profile.Approve());
        using var client = factory.CreateClientFor(1_010_900);

        var response = await client.GetAsync($"/api/bloggers/{profile.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [IntegrationFact]
    public async Task Pending_blogger_details_return_not_found()
    {
        var profile = await AddProfileAsync(1_010_002, _ => { });
        using var client = factory.CreateClientFor(1_010_900);

        var response = await client.GetAsync($"/api/bloggers/{profile.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [IntegrationFact]
    public async Task Rejected_blogger_details_return_not_found()
    {
        var profile = await AddProfileAsync(1_010_003, profile => profile.Reject());
        using var client = factory.CreateClientFor(1_010_900);

        var response = await client.GetAsync($"/api/bloggers/{profile.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [IntegrationFact]
    public async Task Deleted_blogger_details_return_not_found()
    {
        var profile = await AddProfileAsync(1_010_004, profile =>
        {
            profile.Approve();
            profile.SoftDelete();
        });
        using var client = factory.CreateClientFor(1_010_900);

        var response = await client.GetAsync($"/api/bloggers/{profile.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // D59: the catalog is for signed-in Telegram users; without the signature even an approved profile is not served.
    [IntegrationFact]
    public async Task Blogger_details_need_a_telegram_sign_in()
    {
        var profile = await AddProfileAsync(1_010_005, profile => profile.Approve());
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/bloggers/{profile.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<BloggerProfile> AddProfileAsync(long telegramUserId, Action<BloggerProfile> mutate)
    {
        var profile = BloggerProfile.Create(telegramUserId, "Public visibility test", "tashkent", ["beauty"]);
        mutate(profile);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        dbContext.BloggerProfiles.Add(profile);
        await dbContext.SaveChangesAsync();
        return profile;
    }
}
