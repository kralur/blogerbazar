using BloggerBazar.Domain.Entities;
using BloggerBazar.Infrastructure.Persistence;

namespace BloggerBazar.Application.Tests.Features.Bloggers;

public sealed class PublicBloggerVisibilityPolicyTests
{
    [Fact]
    public void Public_catalog_visibility_returns_only_approved_non_deleted_profiles()
    {
        var approved = BloggerProfile.Create(1, "Approved", "tashkent-city", ["beauty"]);
        approved.Approve();

        var pending = BloggerProfile.Create(2, "Pending", "tashkent-city", ["beauty"]);

        var rejected = BloggerProfile.Create(3, "Rejected", "tashkent-city", ["beauty"]);
        rejected.Reject();

        var deleted = BloggerProfile.Create(4, "Deleted", "tashkent-city", ["beauty"]);
        deleted.Approve();
        deleted.SoftDelete();

        var profiles = new[] { approved, pending, rejected, deleted };
        var visible = MarketplaceCatalogVisibility.PublicBloggers(profiles.AsQueryable()).ToArray();

        Assert.Collection(visible, profile => Assert.Equal(approved.Id, profile.Id));
        Assert.DoesNotContain(visible, profile => profile.Id == pending.Id);
        Assert.DoesNotContain(visible, profile => profile.Id == rejected.Id);
        Assert.DoesNotContain(visible, profile => profile.Id == deleted.Id);
    }
}
