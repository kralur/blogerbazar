namespace BloggerBazar.Application.Abstractions.Media;

public enum ProfileMediaTarget
{
    Blogger,
    BrandFace,
    Business
}

public sealed record ProfileMediaUpload(
    ProfileMediaTarget Target,
    Guid ProfileId,
    ReadOnlyMemory<byte> Content,
    string FileName,
    string ContentType);

public sealed record StoredProfileMedia(string PublicUrl);

public interface IProfileMediaStorage
{
    Task<StoredProfileMedia> UploadAsync(ProfileMediaUpload upload, CancellationToken cancellationToken);
    Task DeleteAsync(string publicUrl, CancellationToken cancellationToken);
}
