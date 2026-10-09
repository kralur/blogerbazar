using BloggerBazar.Application.Abstractions.Media;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Features.BrandFaces;
using BloggerBazar.Application.Tests.Features.Deals;
using BloggerBazar.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace BloggerBazar.Application.Tests.Features.BrandFaces;

// QA Q20: the brand face gallery keeps up to four photos besides the avatar; files live in object storage.
public sealed class BrandFacePhotosTests
{
    [Fact]
    public async Task Adds_an_uploaded_photo_to_the_own_gallery()
    {
        var profile = BrandFaceProfile.Create(12, "Madina", "tashkent", ["beauty"]);
        var storage = new FakeStorage();
        var handler = new AddBrandFacePhotoHandler(new FakeBrandFaces(profile), storage, new SpyUnitOfWork(), NullLogger<AddBrandFacePhotoHandler>.Instance);

        var result = await handler.Handle(new AddBrandFacePhotoCommand(12, new byte[] { 1 }, "a.png", "image/png"), CancellationToken.None);

        var url = Assert.Single(result.PhotoUrls);
        Assert.Equal(storage.Uploaded.Single(), url);
        Assert.Equal(ProfileMediaTarget.BrandFace, storage.Targets.Single());
    }

    [Fact]
    public async Task Refuses_a_fifth_photo_before_uploading_anything()
    {
        var profile = BrandFaceProfile.Create(12, "Madina", "tashkent", ["beauty"]);
        for (var index = 0; index < BrandFaceProfile.MaxPhotos; index++) profile.AddPhoto($"https://cdn.example/{index}.webp");
        var storage = new FakeStorage();
        var handler = new AddBrandFacePhotoHandler(new FakeBrandFaces(profile), storage, new SpyUnitOfWork(), NullLogger<AddBrandFacePhotoHandler>.Instance);

        var error = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => handler.Handle(new AddBrandFacePhotoCommand(12, new byte[] { 1 }, "a.png", "image/png"), CancellationToken.None));

        Assert.Equal("photo_limit", error.Code);
        Assert.Empty(storage.Uploaded);
    }

    [Fact]
    public async Task Removes_only_a_photo_of_the_own_gallery_from_storage()
    {
        var profile = BrandFaceProfile.Create(12, "Madina", "tashkent", ["beauty"]);
        profile.AddPhoto("https://cdn.example/own.webp");
        var storage = new FakeStorage();
        var handler = new RemoveBrandFacePhotoHandler(new FakeBrandFaces(profile), storage, new SpyUnitOfWork(), NullLogger<RemoveBrandFacePhotoHandler>.Instance);

        var foreign = await handler.Handle(new RemoveBrandFacePhotoCommand(12, "https://cdn.example/someone-else.webp"), CancellationToken.None);
        var own = await handler.Handle(new RemoveBrandFacePhotoCommand(12, "https://cdn.example/own.webp"), CancellationToken.None);

        Assert.Single(foreign.PhotoUrls);
        Assert.Empty(own.PhotoUrls);
        Assert.Equal(["https://cdn.example/own.webp"], storage.Deleted);
    }

    [Fact]
    public async Task Without_a_brand_face_profile_the_gallery_is_missing()
    {
        var handler = new AddBrandFacePhotoHandler(new FakeBrandFaces(), new FakeStorage(), new SpyUnitOfWork(), NullLogger<AddBrandFacePhotoHandler>.Instance);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new AddBrandFacePhotoCommand(12, new byte[] { 1 }, "a.png", "image/png"), CancellationToken.None));

        Assert.Contains("not found", error.Message);
    }

    private sealed class FakeStorage : IProfileMediaStorage
    {
        public List<string> Uploaded { get; } = [];
        public List<ProfileMediaTarget> Targets { get; } = [];
        public List<string> Deleted { get; } = [];

        public Task<StoredProfileMedia> UploadAsync(ProfileMediaUpload upload, CancellationToken cancellationToken)
        {
            var url = $"https://cdn.example/{Guid.NewGuid():N}.webp";
            Uploaded.Add(url);
            Targets.Add(upload.Target);
            return Task.FromResult(new StoredProfileMedia(url));
        }

        public Task DeleteAsync(string publicUrl, CancellationToken cancellationToken)
        {
            Deleted.Add(publicUrl);
            return Task.CompletedTask;
        }
    }
}
