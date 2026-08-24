using System.Reflection;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using BloggerBazar.Infrastructure.Persistence;

namespace BloggerBazar.Application.Tests.Features.Campaigns;

public sealed class CampaignCatalogReadModelPolicyTests
{
    [Fact]
    public void Public_catalog_uses_the_same_visibility_policy_as_public_details()
    {
        var approved = ApprovedBusiness(1, "Visible");
        var draftBusiness = ApprovedBusiness(2, "Draft");
        var pending = BusinessProfile.Create(3, "Pending", "tashkent");
        var blocked = ApprovedBusiness(4, "Blocked");
        var blockedOwner = PlatformUser.Create(blocked.TelegramUserId, "Blocked", null);
        blockedOwner.SetBlocked(true);

        var visible = PublishedCampaign(approved, "Visible", null, null, null);
        var draft = Campaign.Create(draftBusiness.Id, "Draft", "Description", ["beauty"], null, null, null, "tashkent", null);
        var pendingBusinessCampaign = PublishedCampaign(pending, "Pending", null, null, null);
        var blockedCampaign = PublishedCampaign(blocked, "Blocked", null, null, null);

        var result = MarketplaceCatalogVisibility.PublicCampaigns(
            new[] { visible, draft, pendingBusinessCampaign, blockedCampaign }.AsQueryable(),
            new[] { approved, draftBusiness, pending, blocked }.AsQueryable(),
            new[] { blockedOwner }.AsQueryable())
            .Select(campaign => campaign.Id)
            .ToArray();

        Assert.Equal([visible.Id], result);
    }

    [Fact]
    public void Sorts_are_stable_and_keep_null_deadlines_and_budgets_last()
    {
        var business = ApprovedBusiness(10, "Business");
        var earliest = PublishedCampaign(business, "Early", 300, 500, DateTime.UnixEpoch.AddDays(1));
        var later = PublishedCampaign(business, "Later", 100, 200, DateTime.UnixEpoch.AddDays(2));
        var missing = PublishedCampaign(business, "Missing", null, null, null);
        var query = new[] { missing, earliest, later }.AsQueryable();

        Assert.Equal([earliest.Id, later.Id, missing.Id], CampaignCatalogSorting.Apply(query, "deadline_asc").Select(campaign => campaign.Id));
        Assert.Equal([later.Id, earliest.Id, missing.Id], CampaignCatalogSorting.Apply(query, "budget_asc").Select(campaign => campaign.Id));
        Assert.Equal([earliest.Id, later.Id, missing.Id], CampaignCatalogSorting.Apply(query, "budget_desc").Select(campaign => campaign.Id));
    }

    [Fact]
    public void City_category_budget_and_deadline_filters_use_real_campaign_values()
    {
        var business = ApprovedBusiness(15, "Business");
        var matching = Campaign.Create(business.Id, "Matching", "Description", ["other:custom"], null, 100, 300, "tashkent", DateTime.UnixEpoch.AddDays(2));
        var otherCity = Campaign.Create(business.Id, "Other city", "Description", ["other:custom"], null, 100, 300, "samarkand", DateTime.UnixEpoch.AddDays(2));
        var otherCategory = Campaign.Create(business.Id, "Other category", "Description", ["tech"], null, 100, 300, "tashkent", DateTime.UnixEpoch.AddDays(2));
        var outsideBudget = Campaign.Create(business.Id, "Outside budget", "Description", ["other:custom"], null, 20, 50, "tashkent", DateTime.UnixEpoch.AddDays(2));
        var missingBudget = Campaign.Create(business.Id, "Missing budget", "Description", ["other:custom"], null, null, null, "tashkent", DateTime.UnixEpoch.AddDays(2));
        var missingDeadline = Campaign.Create(business.Id, "Missing deadline", "Description", ["other:custom"], null, 100, 300, "tashkent", null);
        var search = new CampaignCatalogSearch(null, "tashkent", "other:custom", 150, 250, DateTime.UnixEpoch.AddDays(1), DateTime.UnixEpoch.AddDays(3), "promoted", 1, 20);

        var result = CampaignCatalogFiltering.Apply(
                new[] { matching, otherCity, otherCategory, outsideBudget, missingBudget, missingDeadline }.AsQueryable(),
                search)
            .Select(campaign => campaign.Id)
            .ToArray();

        Assert.Equal([matching.Id], result);
    }

    [Theory]
    [InlineData(20, 1, 20, false)]
    [InlineData(21, 1, 20, true)]
    [InlineData(40, 2, 20, false)]
    public void Has_more_is_derived_from_total_without_loading_more_items(int total, int page, int pageSize, bool expected)
    {
        Assert.Equal(expected, CampaignCatalogPagination.HasMore(total, page, pageSize));
    }

    [Fact]
    public void Promoted_and_newest_sorts_have_id_tie_breakers()
    {
        var business = ApprovedBusiness(20, "Business");
        var first = PublishedCampaign(business, "First", null, null, null);
        var second = PublishedCampaign(business, "Second", null, null, null);
        SetPrivateProperty(first, nameof(Campaign.CreatedAtUtc), DateTime.UnixEpoch);
        SetPrivateProperty(second, nameof(Campaign.CreatedAtUtc), DateTime.UnixEpoch);
        SetPrivateProperty(first, nameof(Campaign.IsPromoted), true);
        SetPrivateProperty(second, nameof(Campaign.IsPromoted), true);

        var expected = new[] { first.Id, second.Id }.OrderBy(id => id).ToArray();
        Assert.Equal(expected, CampaignCatalogSorting.Apply(new[] { second, first }.AsQueryable(), "promoted").Select(campaign => campaign.Id));
        Assert.Equal(expected, CampaignCatalogSorting.Apply(new[] { second, first }.AsQueryable(), "newest").Select(campaign => campaign.Id));
    }

    private static BusinessProfile ApprovedBusiness(long telegramUserId, string name)
    {
        var business = BusinessProfile.Create(telegramUserId, name, "tashkent");
        business.Approve();
        return business;
    }

    private static Campaign PublishedCampaign(BusinessProfile business, string title, int? budgetFrom, int? budgetTo, DateTime? deadline)
    {
        var campaign = Campaign.Create(business.Id, title, "Description", ["beauty"], null, budgetFrom, budgetTo, "tashkent", deadline);
        campaign.Publish();
        return campaign;
    }

    private static void SetPrivateProperty<T>(Campaign campaign, string name, T value) =>
        typeof(Campaign).GetProperty(name, BindingFlags.Instance | BindingFlags.Public)!.SetValue(campaign, value);
}
