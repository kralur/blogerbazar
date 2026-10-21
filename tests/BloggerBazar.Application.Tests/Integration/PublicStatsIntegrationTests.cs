using System.Net;
using System.Text.Json;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BloggerBazar.Application.Tests.Integration;

// D58: the landing page totals on a real database. Other tests share the database, so the counts are compared
// before and after this test's own rows, read straight from the read model (the API answer is cached).
[Collection(ApiHostCollection.Name)]
public sealed class PublicStatsIntegrationTests(BloggerBazarApiFactory factory) : IClassFixture<BloggerBazarApiFactory>
{
    [IntegrationFact]
    public async Task Only_approved_profiles_of_active_accounts_count()
    {
        var before = await ReadAsync();

        await AddBloggerAsync(1_600_001, approve: true);
        await AddBloggerAsync(1_600_002, approve: false);
        await AddBloggerAsync(1_600_003, approve: true, blockOwner: true);
        await AddBloggerAsync(1_600_004, approve: true, delete: true);

        var after = await ReadAsync();
        Assert.Equal(before.Bloggers + 1, after.Bloggers);
        Assert.Equal(before.Businesses, after.Businesses);
        Assert.Equal(before.CompletedDeals, after.CompletedDeals);
    }

    [IntegrationFact]
    public async Task Anyone_can_read_the_totals_without_signing_in()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/public/stats");
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=600", response.Headers.CacheControl?.ToString());
        Assert.Equal(["bloggers", "businesses", "completedDeals"], body.EnumerateObject().Select(property => property.Name));
        Assert.All(body.EnumerateObject(), property => Assert.True(property.Value.GetInt32() >= 0));
    }

    private async Task<Application.Features.PublicStats.PublicStatsDto> ReadAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IPublicStatsReadModel>().GetAsync(CancellationToken.None);
    }

    private async Task AddBloggerAsync(long telegramUserId, bool approve, bool blockOwner = false, bool delete = false)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        var user = PlatformUser.Create(telegramUserId, "Stats test", null);
        if (blockOwner) user.SetBlocked(true);
        var profile = BloggerProfile.Create(telegramUserId, "Stats test", "tashkent", ["beauty"]);
        if (approve) profile.Approve();
        if (delete) profile.SoftDelete();
        dbContext.PlatformUsers.Add(user);
        dbContext.BloggerProfiles.Add(profile);
        await dbContext.SaveChangesAsync();
    }
}
