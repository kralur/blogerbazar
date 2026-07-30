using System.Reflection;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Infrastructure.Persistence;

namespace BloggerBazar.Application.Tests.Infrastructure;

public sealed class DevelopmentDataSeederTests
{
    private static readonly BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    [Fact]
    public void Creates_a_complete_and_unique_v050_marketplace_dataset()
    {
        var bloggers = Invoke<List<BloggerProfile>>("CreateBloggers");
        var businesses = Invoke<List<BusinessProfile>>("CreateBusinesses");
        var campaigns = Invoke<List<Campaign>>("CreateCampaigns", businesses);
        var applications = Invoke<List<CampaignApplication>>("CreateApplications", campaigns, bloggers);
        var deals = Invoke<List<Deal>>("CreateCompletedDeals", applications, campaigns);
        var reviews = Invoke<List<Review>>("CreateReviews", deals, bloggers, businesses);

        Assert.Equal(360, bloggers.Count);
        Assert.Equal(120, businesses.Count);
        Assert.Equal(360, campaigns.Count);
        Assert.Equal(1_200, applications.Count);
        Assert.Equal(500, deals.Count);
        Assert.Equal(1_000, reviews.Count);
        Assert.Equal(1_200, applications.Select(application => (application.CampaignId, application.BloggerId)).Distinct().Count());
        Assert.Equal(11, bloggers.Select(blogger => blogger.City).Distinct().Count());
        Assert.Equal(17, bloggers.SelectMany(blogger => blogger.Categories).Distinct().Count());
        Assert.Equal(360, bloggers.Select(blogger => blogger.Username).Distinct().Count());
        Assert.All(bloggers, blogger => Assert.True(blogger.IsVerified));
        Assert.Contains(bloggers, blogger => blogger.IsPromoted);
        Assert.Contains(campaigns, campaign => campaign.IsPromoted);
    }

    private static T Invoke<T>(string methodName, params object[] arguments)
    {
        var method = typeof(DevelopmentDataSeeder).GetMethod(methodName, StaticPrivate)
            ?? throw new InvalidOperationException($"Seed method {methodName} was not found.");
        return (T)(method.Invoke(null, arguments)
            ?? throw new InvalidOperationException($"Seed method {methodName} returned null."));
    }
}
