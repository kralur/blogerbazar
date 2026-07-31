namespace BloggerBazar.Infrastructure.Configuration;

public sealed class ProfileMediaOptions
{
    public const string SectionName = "ProfileMedia";
    public const long DefaultMaxFileSizeBytes = 5 * 1024 * 1024;

    public string? ServiceUrl { get; init; }
    public string? PublicBaseUrl { get; init; }
    public string? Bucket { get; init; }
    public string? AccessKey { get; init; }
    public string? SecretKey { get; init; }
    public string Region { get; init; } = "auto";
    public bool ForcePathStyle { get; init; } = true;
    public long MaxFileSizeBytes { get; init; } = DefaultMaxFileSizeBytes;
    public int MaxImageDimension { get; init; } = 1600;
    public long MaxImagePixels { get; init; } = 25_000_000;
    public int WebpQuality { get; init; } = 85;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ServiceUrl) &&
        !string.IsNullOrWhiteSpace(PublicBaseUrl) &&
        !string.IsNullOrWhiteSpace(Bucket) &&
        !string.IsNullOrWhiteSpace(AccessKey) &&
        !string.IsNullOrWhiteSpace(SecretKey);
}
