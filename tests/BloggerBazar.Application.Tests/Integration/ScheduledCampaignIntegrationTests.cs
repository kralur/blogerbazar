using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using BloggerBazar.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BloggerBazar.Application.Tests.Integration;

// D69: a campaign planned for later waits as a draft and the job publishes it once its time has come.
[Collection(ApiHostCollection.Name)]
public sealed class ScheduledCampaignIntegrationTests(BloggerBazarApiFactory factory) : IClassFixture<BloggerBazarApiFactory>
{
    [IntegrationFact]
    public async Task A_planned_campaign_waits_as_a_draft_and_is_published_when_its_time_comes()
    {
        const long owner = 1_710_001;
        await SeedCompanyAsync(owner, "Planned cafe");
        using var app = factory.CreateClientFor(owner);
        var at = DateTime.UtcNow.AddHours(3);

        var created = await app.PostAsJsonAsync("/api/campaigns", new
        {
            title = "Autumn menu",
            description = "Five stories about the new menu",
            city = "tashkent",
            categories = new[] { "food" },
            publishImmediately = false,
            scheduledPublishAtUtc = at
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var campaignId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var mine = await app.GetFromJsonAsync<JsonElement>($"/api/campaigns/mine/{campaignId}");
        Assert.Equal((int)CampaignStatus.Draft, mine.GetProperty("status").GetInt32());
        Assert.Equal(at, mine.GetProperty("scheduledPublishAtUtc").GetDateTime().ToUniversalTime(), TimeSpan.FromMilliseconds(1));

        // Not yet: the job leaves it alone.
        Assert.Equal(0, await RunJobAsync(DateTime.UtcNow));
        Assert.Equal(CampaignStatus.Draft, await StatusAsync(campaignId));

        // Its time has come: published once, even when two instances run the job.
        Assert.Equal(1, await RunJobAsync(at.AddMinutes(1)));
        Assert.Equal(0, await RunJobAsync(at.AddMinutes(1)));
        Assert.Equal(CampaignStatus.Published, await StatusAsync(campaignId));
        var after = await app.GetFromJsonAsync<JsonElement>($"/api/campaigns/mine/{campaignId}");
        Assert.Equal(JsonValueKind.Null, after.GetProperty("scheduledPublishAtUtc").ValueKind);
    }

    [IntegrationFact]
    public async Task A_cancelled_plan_is_not_published_and_another_company_gets_not_found()
    {
        const long owner = 1_710_011;
        const long stranger = 1_710_012;
        await SeedCompanyAsync(owner, "Cancelling shop");
        await SeedCompanyAsync(stranger, "Other shop");
        using var app = factory.CreateClientFor(owner);
        using var other = factory.CreateClientFor(stranger);

        var created = await app.PostAsJsonAsync("/api/campaigns", new
        {
            title = "Draft promo",
            description = "Later",
            categories = new[] { "beauty" },
            publishImmediately = false
        });
        var campaignId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var at = DateTime.UtcNow.AddHours(1);

        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/campaigns/mine/{campaignId}/schedule", new { publishAtUtc = at })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.PostAsJsonAsync($"/api/campaigns/mine/{campaignId}/schedule", new { publishAtUtc = at })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/campaigns/mine/{campaignId}/schedule")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.DeleteAsync($"/api/campaigns/mine/{campaignId}/schedule")).StatusCode);

        Assert.Equal(0, await RunJobAsync(at.AddMinutes(1)));
        Assert.Equal(CampaignStatus.Draft, await StatusAsync(campaignId));
    }

    [IntegrationFact]
    public async Task A_time_in_the_past_is_rejected()
    {
        const long owner = 1_710_021;
        await SeedCompanyAsync(owner, "Past shop");
        using var app = factory.CreateClientFor(owner);
        var created = await app.PostAsJsonAsync("/api/campaigns", new { title = "Promo", description = "Text", categories = new[] { "food" }, publishImmediately = false });
        var campaignId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var response = await app.PostAsJsonAsync($"/api/campaigns/mine/{campaignId}/schedule", new { publishAtUtc = DateTime.UtcNow.AddHours(-1) });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private async Task<int> RunJobAsync(DateTime nowUtc)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new PublishScheduledCampaignsCommand(nowUtc));
    }

    private async Task<CampaignStatus> StatusAsync(Guid campaignId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        return await dbContext.Campaigns.AsNoTracking().Where(campaign => campaign.Id == campaignId).Select(campaign => campaign.Status).SingleAsync();
    }

    private async Task SeedCompanyAsync(long owner, string name)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        var user = PlatformUser.Create(owner, name, null);
        user.SelectMarketplaceRole(MarketplaceRole.Business);
        user.VerifyPhone($"+99890{owner % 10_000_000:D7}", DateTime.UtcNow);
        dbContext.Add(user);
        await dbContext.SaveChangesAsync();
        var company = BusinessProfile.Create(owner, name, "tashkent");
        company.Approve(verified: false);
        dbContext.Add(company);
        await dbContext.SaveChangesAsync();
    }
}
