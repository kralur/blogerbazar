using BloggerBazar.Domain.Entities;
using BloggerBazar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Application.Tests.Features.Favorites;

public sealed class BrandFaceFavoriteModelTests
{
    [Fact]
    public void Brand_face_favorites_have_required_foreign_keys_and_indexes()
    {
        var options = new DbContextOptionsBuilder<BloggerBazarDbContext>()
            .UseNpgsql("Host=localhost;Database=bloggerbazar_tests;Username=postgres;Password=postgres")
            .Options;
        using var dbContext = new BloggerBazarDbContext(options);
        var entity = dbContext.Model.FindEntityType(typeof(BrandFaceFavorite))!;

        Assert.Contains(entity.GetForeignKeys(), foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(PlatformUser) && foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
        Assert.Contains(entity.GetForeignKeys(), foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(BrandFaceProfile) && foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(BrandFaceFavorite.PlatformUserId), nameof(BrandFaceFavorite.BrandFaceId)]));
        Assert.Contains(entity.GetIndexes(), index => index.Properties.Select(property => property.Name).SequenceEqual([nameof(BrandFaceFavorite.PlatformUserId), nameof(BrandFaceFavorite.CreatedAtUtc), nameof(BrandFaceFavorite.Id)]));
    }
}
