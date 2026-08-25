using System.Reflection;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using BloggerBazar.Infrastructure.Persistence;

namespace BloggerBazar.Application.Tests.Features.Campaigns;

public sealed class CampaignManagementReadModelPolicyTests
{
    [Fact]
    public void Ownership_scope_excludes_foreign_campaigns()
    {
        var owner = BusinessProfile.Create(1, "Owner", "tashkent");
        var foreignBusiness = BusinessProfile.Create(2, "Foreign", "samarkand");
        var owned = Campaign.Create(owner.Id, "Owned", "Description", ["beauty"], null, null, null, "tashkent", null);
        var foreign = Campaign.Create(foreignBusiness.Id, "Foreign", "Description", ["fashion"], null, null, null, "samarkand", null);

        var result = CampaignManagementOwnership.ForBusiness(new[] { owned, foreign }.AsQueryable(), owner.Id).ToArray();

        Assert.Collection(result, campaign => Assert.Equal(owned.Id, campaign.Id));
    }

    [Theory]
    [InlineData(CampaignStatus.Draft)]
    [InlineData(CampaignStatus.Published)]
    [InlineData(CampaignStatus.Archived)]
    [InlineData(CampaignStatus.Rejected)]
    public void Status_filter_returns_only_the_requested_existing_lifecycle_status(CampaignStatus status)
    {
        var business = BusinessProfile.Create(3, "Business", "tashkent");
        var campaigns = Enum.GetValues<CampaignStatus>().Select(item => CampaignWithStatus(business, item)).AsQueryable();
        var search = new MyCampaignsSearch((int)status, null, "newest", 1, 20);

        var result = CampaignManagementFiltering.Apply(campaigns, search).ToArray();

        Assert.Collection(result, campaign => Assert.Equal(status, campaign.Status));
    }

    [Theory]
    [InlineData("newest")]
    [InlineData("oldest")]
    [InlineData("deadline_asc")]
    [InlineData("deadline_desc")]
    [InlineData("budget_asc")]
    [InlineData("budget_desc")]
    public void Each_management_sort_is_stable_and_keeps_nulls_last(string sort)
    {
        var business = BusinessProfile.Create(4, "Business", "tashkent");
        var first = Campaign.Create(business.Id, "First", "Description", ["beauty"], null, 100, 200, "tashkent", DateTime.UnixEpoch.AddDays(1));
        var second = Campaign.Create(business.Id, "Second", "Description", ["beauty"], null, 100, 200, "tashkent", DateTime.UnixEpoch.AddDays(1));
        var missing = Campaign.Create(business.Id, "Missing", "Description", ["beauty"], null, null, null, "tashkent", null);
        SetPrivateProperty(first, nameof(Campaign.CreatedAtUtc), DateTime.UnixEpoch);
        SetPrivateProperty(second, nameof(Campaign.CreatedAtUtc), DateTime.UnixEpoch);
        SetPrivateProperty(missing, nameof(Campaign.CreatedAtUtc), DateTime.UnixEpoch);

        var result = CampaignManagementSorting.Apply(new[] { second, missing, first }.AsQueryable(), sort).ToArray();

        if (sort is "deadline_asc" or "deadline_desc" or "budget_asc" or "budget_desc")
        {
            Assert.Equal(new[] { first.Id, second.Id }.OrderBy(id => id), result.Take(2).Select(campaign => campaign.Id));
            Assert.Equal(missing.Id, result[^1].Id);
        }
        else
        {
            Assert.Equal(new[] { first.Id, second.Id, missing.Id }.OrderBy(id => id), result.Select(campaign => campaign.Id));
        }
    }

    [Fact]
    public void Management_details_projection_preserves_editable_values()
    {
        var business = BusinessProfile.Create(5, "Business", "tashkent");
        var campaign = Campaign.Create(business.Id, "Campaign", "Description", ["beauty"], ["brief"], 100, 200, "tashkent", DateTime.UnixEpoch);
        campaign.Publish();

        var result = CampaignManagementProjection.Details(new[] { campaign }.AsQueryable()).Single();

        Assert.Equal(campaign.Title, result.Title);
        Assert.Equal(campaign.Description, result.Description);
        Assert.Equal(campaign.Categories, result.Categories);
        Assert.Equal(campaign.Requirements, result.Requirements);
        Assert.Equal(campaign.BudgetFrom, result.MinBudget);
        Assert.Equal(campaign.BudgetTo, result.MaxBudget);
        Assert.Equal(campaign.Deadline, result.Deadline);
        Assert.Equal((int)CampaignStatus.Published, result.Status);
    }

    private static Campaign CampaignWithStatus(BusinessProfile business, CampaignStatus status)
    {
        var campaign = Campaign.Create(business.Id, status.ToString(), "Description", ["beauty"], null, null, null, "tashkent", null);
        campaign.SetStatus(status);
        return campaign;
    }

    private static void SetPrivateProperty<T>(Campaign campaign, string name, T value) =>
        typeof(Campaign).GetProperty(name, BindingFlags.Instance | BindingFlags.Public)!.SetValue(campaign, value);
}
