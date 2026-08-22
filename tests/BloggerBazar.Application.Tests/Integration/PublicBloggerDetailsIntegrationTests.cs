using System.Net;
using BloggerBazar.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using BloggerBazar.Infrastructure.Persistence;

namespace BloggerBazar.Application.Tests.Integration;

public sealed class PublicBloggerDetailsIntegrationTests(BloggerBazarApiFactory factory) : IClassFixture<BloggerBazarApiFactory>
{
    [IntegrationFact]
    public async Task Approved_blogger_details_are_publicly_available()
    {
        var profile = await AddProfileAsync(1_010_001, profile => profile.Approve());
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/bloggers/{profile.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [IntegrationFact]
    public async Task Pending_blogger_details_return_not_found()
    {
        var profile = await AddProfileAsync(1_010_002, _ => { });
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/bloggers/{profile.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [IntegrationFact]
    public async Task Rejected_blogger_details_return_not_found()
    {
        var profile = await AddProfileAsync(1_010_003, profile => profile.Reject());
        using var client = factory.CreateClient();

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
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/bloggers/{profile.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
