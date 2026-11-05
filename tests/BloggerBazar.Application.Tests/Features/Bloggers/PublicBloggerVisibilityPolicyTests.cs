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

        // Pre-launch audit 2026-11-04: an approved profile of a blocked account is not public either.
        var blockedOwner = BloggerProfile.Create(5, "Blocked owner", "tashkent-city", ["beauty"]);
        blockedOwner.Approve();
        var blockedUser = PlatformUser.Create(5, "Blocked", null);
        blockedUser.SetBlocked(true);

        var profiles = new[] { approved, pending, rejected, deleted, blockedOwner };
        var visible = MarketplaceCatalogVisibility.PublicBloggers(profiles.AsQueryable(), new[] { PlatformUser.Create(1, "Approved", null), blockedUser }.AsQueryable()).ToArray();

        Assert.Collection(visible, profile => Assert.Equal(approved.Id, profile.Id));
        Assert.DoesNotContain(visible, profile => profile.Id == blockedOwner.Id);
        Assert.DoesNotContain(visible, profile => profile.Id == pending.Id);
        Assert.DoesNotContain(visible, profile => profile.Id == rejected.Id);
        Assert.DoesNotContain(visible, profile => profile.Id == deleted.Id);
    }
}
