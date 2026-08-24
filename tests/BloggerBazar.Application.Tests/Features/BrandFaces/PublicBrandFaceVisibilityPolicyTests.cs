using BloggerBazar.Domain.Entities;
using BloggerBazar.Infrastructure.Persistence;

namespace BloggerBazar.Application.Tests.Features.BrandFaces;

public sealed class PublicBrandFaceVisibilityPolicyTests
{
    [Fact]
    public void Public_catalog_and_details_visibility_hides_deleted_and_unavailable_owners()
    {
        var active = BrandFaceProfile.Create(1, "Active", "tashkent", ["beauty"]);
        var deletedProfile = BrandFaceProfile.Create(2, "Deleted profile", "tashkent", ["beauty"]);
        deletedProfile.SoftDelete();
        var blockedOwner = BrandFaceProfile.Create(3, "Blocked owner", "tashkent", ["beauty"]);
        var deletedOwner = BrandFaceProfile.Create(4, "Deleted owner", "tashkent", ["beauty"]);

        var activeUser = PlatformUser.Create(1, "Active", "active");
        var blockedUser = PlatformUser.Create(3, "Blocked", "blocked");
        blockedUser.SetBlocked(true);
        var removedUser = PlatformUser.Create(4, "Deleted", "deleted");
        removedUser.SoftDelete(4);

        var visible = BrandFaceCatalogVisibility.PublicBrandFaces(
                new[] { active, deletedProfile, blockedOwner, deletedOwner }.AsQueryable(),
                new[] { activeUser, blockedUser, removedUser }.AsQueryable())
            .ToArray();

        Assert.Collection(visible, profile => Assert.Equal(active.Id, profile.Id));
    }

    [Fact]
    public void Sorting_keeps_null_prices_last_and_uses_id_as_a_stable_tie_breaker()
    {
        var first = CreateProfile(1, 250_000);
        var second = CreateProfile(2, 250_000);
        var missingPrice = CreateProfile(3, null);
        var cheapest = CreateProfile(4, 100_000);
        var profiles = new[] { first, second, missingPrice, cheapest }.AsQueryable();

        var ascending = BrandFaceCatalogSorting.Apply(profiles, "price_asc").Select(profile => profile.Id).ToArray();
        var descending = BrandFaceCatalogSorting.Apply(profiles, "price_desc").Select(profile => profile.Id).ToArray();

        Assert.Equal(cheapest.Id, ascending[0]);
        Assert.Equal(missingPrice.Id, ascending[^1]);
        Assert.Equal(first.Id < second.Id ? new[] { first.Id, second.Id } : new[] { second.Id, first.Id }, ascending.Skip(1).Take(2));
        Assert.Equal(missingPrice.Id, descending[^1]);
        Assert.Equal(first.Id < second.Id ? new[] { first.Id, second.Id } : new[] { second.Id, first.Id }, descending.Take(2));
    }

    [Theory]
    [InlineData(40, 1, 20, true)]
    [InlineData(40, 2, 20, false)]
    [InlineData(39, 1, 20, true)]
    public void Pagination_has_more_matches_total(int total, int page, int pageSize, bool expected)
    {
        Assert.Equal(expected, BrandFaceCatalogPagination.HasMore(total, page, pageSize));
    }

    private static BrandFaceProfile CreateProfile(long telegramUserId, int? collaborationPrice)
    {
        var profile = BrandFaceProfile.Create(telegramUserId, $"Face {telegramUserId}", "tashkent", ["beauty"]);
        profile.Update($"Face {telegramUserId}", "tashkent", null, null, ["uz"], ["beauty"], null, null, "@face", null, collaborationPrice, null, null);
        return profile;
    }
}
