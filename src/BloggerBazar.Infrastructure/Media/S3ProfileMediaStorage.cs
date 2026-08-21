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
            logger.LogError(
                "Profile media upload failed. StatusCode {StatusCode}; ErrorCode {ErrorCode}; RequestId {RequestId}",
                (int)exception.StatusCode,
                exception.ErrorCode ?? "unknown",
                exception.RequestId ?? "unknown");
            throw new ProfileMediaStorageUnavailableException();
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(
                "Profile media upload could not reach object storage. StatusCode {StatusCode}",
                exception.StatusCode is null ? "unknown" : ((int)exception.StatusCode.Value).ToString());
            throw new ProfileMediaStorageUnavailableException();
        }

        return new StoredProfileMedia(BuildPublicUrl(_options.PublicBaseUrl!, objectKey));
    }

    public async Task DeleteAsync(string publicUrl, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured || !TryGetManagedObjectKey(_options.PublicBaseUrl!, publicUrl, out var objectKey))
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
            logger.LogWarning(
                "Profile media delete failed. StatusCode {StatusCode}; ErrorCode {ErrorCode}; RequestId {RequestId}",
                (int)exception.StatusCode,
                exception.ErrorCode ?? "unknown",
                exception.RequestId ?? "unknown");
            throw new ProfileMediaStorageUnavailableException();
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                "Profile media delete could not reach object storage. StatusCode {StatusCode}",
                exception.StatusCode is null ? "unknown" : ((int)exception.StatusCode.Value).ToString());
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
        CreateS3Config(_options));

    internal static AmazonS3Config CreateS3Config(ProfileMediaOptions options) => new()
    {
        ServiceURL = options.ServiceUrl,
        AuthenticationRegion = options.Region,
        ForcePathStyle = options.ForcePathStyle
    };

    internal static string BuildPublicUrl(string publicBaseUrl, string objectKey) => $"{publicBaseUrl.TrimEnd('/')}/{objectKey}";

    internal static bool TryGetManagedObjectKey(string publicBaseUrl, string publicUrl, out string objectKey)
    {
        var prefix = $"{publicBaseUrl.TrimEnd('/')}/";
        if (!publicUrl.StartsWith(prefix, StringComparison.Ordinal))
        {
            objectKey = string.Empty;
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
