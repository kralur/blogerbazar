using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using BloggerBazar.Application.Abstractions.Media;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace BloggerBazar.Infrastructure.Media;

internal sealed class S3ProfileMediaStorage(
    IOptions<ProfileMediaOptions> options,
    ILogger<S3ProfileMediaStorage> logger) : IProfileMediaStorage
{
    private static readonly HashSet<string> SupportedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp"
    };

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private readonly ProfileMediaOptions _options = options.Value;

    public async Task<StoredProfileMedia> UploadAsync(ProfileMediaUpload upload, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        ValidateFileMetadata(upload);

        using var input = new MemoryStream(upload.Content.ToArray(), writable: false);
        using var image = LoadAndValidateImage(input);
        using var resizedImage = ResizeImage(image);
        using var encodedImage = resizedImage.Encode(SKEncodedImageFormat.Webp, _options.WebpQuality);
        await using var output = new MemoryStream();
        encodedImage.SaveTo(output);
        output.Position = 0;

        var objectKey = $"profiles/{ToStorageSegment(upload.Target)}/{upload.ProfileId:N}/{Guid.NewGuid():N}.webp";
        try
        {
            using var client = CreateClient();
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _options.Bucket,
                Key = objectKey,
                InputStream = output,
                ContentType = "image/webp"
            }, cancellationToken);
        }
        catch (AmazonS3Exception exception)
        {
            logger.LogError(exception, "Profile media upload failed with storage error {StorageErrorCode}", exception.ErrorCode);
            throw new ProfileMediaStorageUnavailableException();
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Profile media upload could not reach object storage");
            throw new ProfileMediaStorageUnavailableException();
        }

        return new StoredProfileMedia(BuildPublicUrl(objectKey));
    }

    public async Task DeleteAsync(string publicUrl, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured || !TryGetManagedObjectKey(publicUrl, out var objectKey))
        {
            return;
        }

        try
        {
            using var client = CreateClient();
            await client.DeleteObjectAsync(_options.Bucket, objectKey, cancellationToken);
        }
        catch (AmazonS3Exception exception)
        {
            logger.LogWarning(exception, "Profile media delete failed with storage error {StorageErrorCode}", exception.ErrorCode);
            throw new ProfileMediaStorageUnavailableException();
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Profile media delete could not reach object storage");
            throw new ProfileMediaStorageUnavailableException();
        }
    }

    private SKBitmap LoadAndValidateImage(Stream input)
    {
        using var codec = SKCodec.Create(input);
        if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0 ||
            (long)codec.Info.Width * codec.Info.Height > _options.MaxImagePixels)
        {
            throw new ProfileMediaValidationException();
        }

        var image = SKBitmap.Decode(codec);
        if (image is null)
        {
            throw new ProfileMediaValidationException();
        }

        return image;
    }

    private SKImage ResizeImage(SKBitmap image)
    {
        var scale = Math.Min(1d, Math.Min(
            (double)_options.MaxImageDimension / image.Width,
            (double)_options.MaxImageDimension / image.Height));
        var width = Math.Max(1, (int)Math.Round(image.Width * scale));
        var height = Math.Max(1, (int)Math.Round(image.Height * scale));
        using var resized = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (!image.ScalePixels(resized, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None)))
        {
            throw new ProfileMediaValidationException();
        }

        return SKImage.FromBitmap(resized);
    }

    private void ValidateFileMetadata(ProfileMediaUpload upload)
    {
        if (upload.Content.IsEmpty || upload.Content.Length > _options.MaxFileSizeBytes ||
            !SupportedContentTypes.Contains(upload.ContentType) ||
            !SupportedExtensions.Contains(Path.GetExtension(upload.FileName)))
        {
            throw new ProfileMediaValidationException();
        }
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
        {
            logger.LogWarning("Profile media storage is not configured");
            throw new ProfileMediaStorageUnavailableException();
        }
    }

    private AmazonS3Client CreateClient() => new(
        new BasicAWSCredentials(_options.AccessKey!, _options.SecretKey!),
        new AmazonS3Config
        {
            ServiceURL = _options.ServiceUrl,
            AuthenticationRegion = _options.Region,
            ForcePathStyle = _options.ForcePathStyle
        });

    private string BuildPublicUrl(string objectKey) => $"{_options.PublicBaseUrl!.TrimEnd('/')}/{objectKey}";

    private bool TryGetManagedObjectKey(string publicUrl, out string objectKey)
    {
        var prefix = $"{_options.PublicBaseUrl!.TrimEnd('/')}/";
        if (!publicUrl.StartsWith(prefix, StringComparison.Ordinal))
        {
            objectKey = string.Empty;
            logger.LogInformation("Skipped profile media cleanup for an unmanaged URL");
            return false;
        }

        objectKey = publicUrl[prefix.Length..];
        return objectKey.StartsWith("profiles/", StringComparison.Ordinal) && objectKey.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToStorageSegment(ProfileMediaTarget target) => target switch
    {
        ProfileMediaTarget.Blogger => "bloggers",
        ProfileMediaTarget.BrandFace => "brand-faces",
        ProfileMediaTarget.Business => "businesses",
        _ => throw new ProfileMediaValidationException()
    };
}
