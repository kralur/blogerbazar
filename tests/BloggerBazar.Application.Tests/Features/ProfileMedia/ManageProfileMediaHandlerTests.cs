using BloggerBazar.Application.Abstractions.Media;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Caching;
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
    public async Task Database_failure_restores_previous_image_and_cleans_up_only_new_object()
    {
        var previousUrl = "https://media.example/profiles/bloggers/old.webp";
        var uploadedUrl = "https://media.example/profiles/bloggers/new.webp";
        var profile = CreateBlogger(previousUrl);
        var storage = new FakeStorage(uploadedUrl);
        var handler = CreateUploadHandler(profile, storage, new SpyUnitOfWork { ThrowOnSave = true });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new UploadProfileMediaCommand(101, ProfileMediaTarget.Blogger, new byte[] { 1 }, "avatar.png", "image/png"), CancellationToken.None));

        Assert.Equal(previousUrl, profile.AvatarUrl);
        Assert.Equal([uploadedUrl], storage.DeletedUrls);
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

    [Fact]
    public async Task Brand_face_media_upload_and_delete_rotate_the_catalog_namespace()
    {
        var profile = BrandFaceProfile.Create(202, "Brand Face", "tashkent", ["beauty"]);
        profile.Update("Brand Face", "tashkent", null, null, ["uz"], ["beauty"], null, null, "@brandface", null, null, null, null);
        var cache = new RecordingCache();
        var storage = new FakeStorage("https://media.example/profiles/brand-faces/new.webp");
        var brandFaces = new BrandFaceRepository(profile);
        var upload = new UploadProfileMediaHandler(new BloggerRepository(CreateBlogger(null)), brandFaces, new EmptyBusinessRepository(), storage, new SpyUnitOfWork(), NullLogger<UploadProfileMediaHandler>.Instance, cache);
        var delete = new DeleteProfileMediaHandler(new BloggerRepository(CreateBlogger(null)), brandFaces, new EmptyBusinessRepository(), storage, new SpyUnitOfWork(), NullLogger<DeleteProfileMediaHandler>.Instance, cache);

        await upload.Handle(new UploadProfileMediaCommand(202, ProfileMediaTarget.BrandFace, new byte[] { 1 }, "avatar.png", "image/png"), CancellationToken.None);
        await delete.Handle(new DeleteProfileMediaCommand(202, ProfileMediaTarget.BrandFace), CancellationToken.None);

        Assert.Equal(2, cache.Rotations);
    }

    private static UploadProfileMediaHandler CreateUploadHandler(BloggerProfile profile, FakeStorage storage, IUnitOfWork? unitOfWork = null) => new(
        new BloggerRepository(profile),
        new EmptyBrandFaceRepository(),
        new EmptyBusinessRepository(),
        storage,
        unitOfWork ?? new SpyUnitOfWork(),
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

    private sealed class BrandFaceRepository(BrandFaceProfile profile) : IBrandFaceProfileRepository
    {
        public Task AddAsync(BrandFaceProfile value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BrandFaceProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BrandFaceProfile?>(id == profile.Id ? profile : null);
        public Task<BrandFaceProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<BrandFaceProfile?>(telegramUserId == profile.TelegramUserId ? profile : null);
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
        public bool ThrowOnSave { get; init; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => ThrowOnSave
            ? Task.FromException<int>(new InvalidOperationException("Database write failed."))
            : Task.FromResult(1);
    }

    private sealed class RecordingCache : ICatalogCache
    {
        public int Rotations { get; private set; }
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class => Task.FromResult<T?>(null);
        public Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken) where T : class => Task.CompletedTask;
        public Task RotateNamespaceVersionAsync(CancellationToken cancellationToken) { Rotations++; return Task.CompletedTask; }
    }
}
