using BloggerBazar.Application.Abstractions.Media;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Infrastructure.Configuration;
using BloggerBazar.Infrastructure.Media;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Application.Tests.Infrastructure;

public sealed class ProfileMediaOptionsTests
{
    private const string PublicBaseUrl = "https://pub.example.r2.dev";
    private static readonly object EnvironmentLock = new();

    [Fact]
    public void Environment_style_configuration_binds_cloudflare_r2_settings()
    {
        var variables = new Dictionary<string, string?>
        {
            ["ProfileMedia__ServiceUrl"] = "https://account.r2.cloudflarestorage.com",
            ["ProfileMedia__PublicBaseUrl"] = PublicBaseUrl,
            ["ProfileMedia__Bucket"] = "bloggerbazar-profile-media",
            ["ProfileMedia__AccessKey"] = "access-key",
            ["ProfileMedia__SecretKey"] = "secret-key",
            ["ProfileMedia__Region"] = "auto",
            ["ProfileMedia__ForcePathStyle"] = "true",
            ["ProfileMedia__MaxFileSizeBytes"] = "5242880",
            ["ProfileMedia__MaxImageDimension"] = "1600",
            ["ProfileMedia__MaxImagePixels"] = "25000000",
            ["ProfileMedia__WebpQuality"] = "85"
        };

        lock (EnvironmentLock)
        {
            var originalValues = variables.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
            try
            {
                foreach (var (key, value) in variables)
                {
                    Environment.SetEnvironmentVariable(key, value);
                }

                var options = new ConfigurationBuilder().AddEnvironmentVariables()
                    .Build().GetSection(ProfileMediaOptions.SectionName).Get<ProfileMediaOptions>()!;
                var clientConfiguration = S3ProfileMediaStorage.CreateS3Config(options);

                Assert.True(options.IsConfigured);
                Assert.Equal("auto", clientConfiguration.AuthenticationRegion);
                Assert.True(clientConfiguration.ForcePathStyle);
                Assert.Equal(RequestChecksumCalculation.WHEN_REQUIRED, clientConfiguration.RequestChecksumCalculation);
                Assert.Equal(ResponseChecksumValidation.WHEN_REQUIRED, clientConfiguration.ResponseChecksumValidation);
                Assert.Equal("https://account.r2.cloudflarestorage.com/", clientConfiguration.ServiceURL);
            }
            finally
            {
                foreach (var (key, value) in originalValues)
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }
    }

    [Fact]
    public void Public_webp_url_is_built_from_the_configured_public_base_url()
    {
        var url = S3ProfileMediaStorage.BuildPublicUrl(
            $"{PublicBaseUrl}/",
            "profiles/bloggers/00000000000000000000000000000000/image.webp");

        Assert.Equal($"{PublicBaseUrl}/profiles/bloggers/00000000000000000000000000000000/image.webp", url);
    }

    [Fact]
    public void R2_put_object_uses_a_known_length_non_chunked_webp_request()
    {
        const string objectKey = "profiles/bloggers/00000000000000000000000000000000/image.webp";
        using var content = new MemoryStream(new byte[1234], writable: false);

        var request = S3ProfileMediaStorage.CreatePutObjectRequest("bloggerbazar-profile-media", objectKey, content);

        Assert.Equal("bloggerbazar-profile-media", request.BucketName);
        Assert.Equal(objectKey, request.Key);
        Assert.Equal(1234, request.Headers.ContentLength);
        Assert.Equal("image/webp", request.ContentType);
        Assert.False(request.UseChunkEncoding);
        Assert.False(request.DisablePayloadSigning);
        Assert.Null(request.ChecksumAlgorithm);
        Assert.Same(content, request.InputStream);
    }

    [Fact]
    public void Unmanaged_urls_are_never_treated_as_storage_objects()
    {
        var isManaged = S3ProfileMediaStorage.TryGetManagedObjectKey(
            PublicBaseUrl,
            "https://external.example/avatar.webp",
            out var objectKey);

        Assert.False(isManaged);
        Assert.Empty(objectKey);
    }

    [Fact]
    public async Task Missing_required_storage_configuration_returns_storage_unavailable()
    {
        var storage = new S3ProfileMediaStorage(
            Options.Create(new ProfileMediaOptions()),
            NullLogger<S3ProfileMediaStorage>.Instance);

        await Assert.ThrowsAsync<ProfileMediaStorageUnavailableException>(() => storage.UploadAsync(
            new ProfileMediaUpload(ProfileMediaTarget.Blogger, Guid.NewGuid(), new byte[] { 1 }, "avatar.png", "image/png"),
            CancellationToken.None));
    }
}
