using BloggerBazar.Application.Abstractions.Media;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Features.ProfileMedia;
using BloggerBazar.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace BloggerBazar.Application.Tests.Features.ProfileMedia;

public sealed class ManageProfileMediaHandlerTests
{
    [Fact]
    public async Task Upload_replaces_primary_image_and_cleans_up_previous_object()
    {
        var profile = CreateBlogger("https://media.example/profiles/bloggers/old.webp");
        var storage = new FakeStorage("https://media.example/profiles/bloggers/new.webp");
        var handler = CreateUploadHandler(profile, storage);

        var result = await handler.Handle(new UploadProfileMediaCommand(101, ProfileMediaTarget.Blogger, new byte[] { 1, 2, 3 }, "avatar.png", "image/png"), CancellationToken.None);

        Assert.Equal("https://media.example/profiles/bloggers/new.webp", result.Url);
        Assert.Equal(result.Url, profile.AvatarUrl);
        Assert.Equal(["https://media.example/profiles/bloggers/old.webp"], storage.DeletedUrls);
    }

    [Fact]
    public async Task Upload_failure_does_not_change_existing_primary_image()
    {
        var previousUrl = "https://media.example/profiles/bloggers/old.webp";
        var profile = CreateBlogger(previousUrl);
        var storage = new FakeStorage("https://media.example/profiles/bloggers/new.webp") { ThrowOnUpload = true };
        var handler = CreateUploadHandler(profile, storage);

        await Assert.ThrowsAsync<ProfileMediaStorageUnavailableException>(() =>
            handler.Handle(new UploadProfileMediaCommand(101, ProfileMediaTarget.Blogger, new byte[] { 1 }, "avatar.png", "image/png"), CancellationToken.None));

        Assert.Equal(previousUrl, profile.AvatarUrl);
        Assert.Empty(storage.DeletedUrls);
    }

    [Fact]
    public async Task Delete_removes_primary_image_and_storage_object()
    {
        var previousUrl = "https://media.example/profiles/bloggers/old.webp";
        var profile = CreateBlogger(previousUrl);
        var storage = new FakeStorage("https://media.example/profiles/bloggers/new.webp");
        var handler = new DeleteProfileMediaHandler(
            new BloggerRepository(profile),
            new EmptyBrandFaceRepository(),
            new EmptyBusinessRepository(),
            storage,
            new SpyUnitOfWork(),
            NullLogger<DeleteProfileMediaHandler>.Instance);

        await handler.Handle(new DeleteProfileMediaCommand(101, ProfileMediaTarget.Blogger), CancellationToken.None);

        Assert.Null(profile.AvatarUrl);
        Assert.Equal([previousUrl], storage.DeletedUrls);
    }

    private static UploadProfileMediaHandler CreateUploadHandler(BloggerProfile profile, FakeStorage storage) => new(
        new BloggerRepository(profile),
        new EmptyBrandFaceRepository(),
        new EmptyBusinessRepository(),
        storage,
        new SpyUnitOfWork(),
        NullLogger<UploadProfileMediaHandler>.Instance);

    private static BloggerProfile CreateBlogger(string? avatarUrl)
    {
        var profile = BloggerProfile.Create(101, "Madina", "tashkent-city", ["beauty"]);
        profile.SetPrimaryImageUrl(avatarUrl);
        return profile;
    }

    private sealed class FakeStorage(string nextUrl) : IProfileMediaStorage
    {
        public bool ThrowOnUpload { get; init; }
        public List<string> DeletedUrls { get; } = [];

        public Task<StoredProfileMedia> UploadAsync(ProfileMediaUpload upload, CancellationToken cancellationToken)
        {
            if (ThrowOnUpload) throw new ProfileMediaStorageUnavailableException();
            return Task.FromResult(new StoredProfileMedia(nextUrl));
        }

        public Task DeleteAsync(string publicUrl, CancellationToken cancellationToken)
        {
            DeletedUrls.Add(publicUrl);
            return Task.CompletedTask;
        }
    }

    private sealed class BloggerRepository(BloggerProfile profile) : IBloggerProfileRepository
    {
        public Task AddAsync(BloggerProfile value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(id == profile.Id ? profile : null);
        public Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(telegramUserId == profile.TelegramUserId ? profile : null);
        public Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
    }

    private sealed class EmptyBrandFaceRepository : IBrandFaceProfileRepository
    {
        public Task AddAsync(BrandFaceProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BrandFaceProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BrandFaceProfile?>(null);
        public Task<BrandFaceProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<BrandFaceProfile?>(null);
        public Task<IReadOnlyList<BrandFaceProfile>> GetAllAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BrandFaceProfile>>([]);
        public Task<IReadOnlyList<BrandFaceProfile>> SearchAsync(string? query, string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BrandFaceProfile>>([]);
    }

    private sealed class EmptyBusinessRepository : IBusinessProfileRepository
    {
        public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BusinessProfile?>(null);
        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<BusinessProfile?>(null);
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
