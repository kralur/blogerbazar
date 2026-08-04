using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.BrandFaces;
using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Tests.Features.BrandFaces;

public sealed class BrandFaceCatalogHandlerTests
{
    [Fact]
    public async Task Public_detail_exposes_the_brand_face_contact()
    {
        var profile = BrandFaceProfile.Create(11, "Madina", "tashkent", ["beauty"]);
        profile.Update("Madina", "tashkent", null, null, ["ru", "uz"], ["beauty"], null, "@madina_inst", "@private_telegram", null, 500000, "Creator", null);
        var handler = new GetBrandFaceHandler(new InMemoryBrandFaceRepository(profile));

        var result = await handler.Handle(new GetBrandFaceQuery(profile.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Madina", result.Name);
        Assert.Equal("@private_telegram", result.Telegram);
    }

    private sealed class InMemoryBrandFaceRepository(params BrandFaceProfile[] profiles) : IBrandFaceProfileRepository
    {
        public Task<BrandFaceProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(profiles.SingleOrDefault(profile => profile.Id == id));
        public Task<BrandFaceProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(profiles.SingleOrDefault(profile => profile.TelegramUserId == telegramUserId));
        public Task<IReadOnlyList<BrandFaceProfile>> GetAllAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BrandFaceProfile>>(profiles.Take(take).ToArray());
        public Task<IReadOnlyList<BrandFaceProfile>> SearchAsync(string? query, string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BrandFaceProfile>>(profiles.Skip(skip).Take(take).ToArray());
        public Task AddAsync(BrandFaceProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
