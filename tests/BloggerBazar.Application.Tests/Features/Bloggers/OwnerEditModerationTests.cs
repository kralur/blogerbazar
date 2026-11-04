using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Bloggers;

// Owner decision 2026-11-04: saving a profile the moderator rejected or returned sends it back to moderation; a
// published profile stays published.
public sealed class OwnerEditModerationTests
{
    [Fact]
    public void A_rejected_or_returned_blogger_goes_back_to_moderation_on_save()
    {
        var rejected = BloggerProfile.Create(1, "Rejected", "tashkent", ["beauty"]);
        rejected.Reject();
        var returned = BloggerProfile.Create(2, "Returned", "tashkent", ["beauty"]);
        returned.RequestChanges();

        rejected.PublishAfterOwnerEdit();
        returned.PublishAfterOwnerEdit();

        Assert.Equal(BloggerStatus.Pending, rejected.Status);
        Assert.Equal(BloggerStatus.Pending, returned.Status);
    }

    [Fact]
    public void A_published_blogger_stays_published_on_save()
    {
        var approved = BloggerProfile.Create(3, "Approved", "tashkent", ["beauty"]);
        approved.Approve(verified: false);

        approved.PublishAfterOwnerEdit();

        Assert.Equal(BloggerStatus.Approved, approved.Status);
    }

    [Fact]
    public void A_rejected_company_goes_back_to_moderation_and_a_published_one_stays()
    {
        var rejected = BusinessProfile.Create(4, "Rejected Co", "tashkent");
        rejected.Reject();
        var approved = BusinessProfile.Create(5, "Approved Co", "tashkent");
        approved.Approve(verified: false);

        rejected.PublishAfterOwnerEdit();
        approved.PublishAfterOwnerEdit();

        Assert.Equal(BloggerStatus.Pending, rejected.ModerationStatus);
        Assert.Equal(BloggerStatus.Approved, approved.ModerationStatus);
    }
}
