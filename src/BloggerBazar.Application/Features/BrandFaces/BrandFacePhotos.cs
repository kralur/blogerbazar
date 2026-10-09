using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Abstractions.Media;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.BrandFaces;

// The brand face's gallery besides the avatar (QA Q20): files live in object storage, the profile keeps only their URLs.
public sealed record BrandFacePhotosDto(IReadOnlyCollection<string> PhotoUrls);

public sealed record AddBrandFacePhotoCommand(long TelegramUserId, ReadOnlyMemory<byte> Content, string FileName, string ContentType) : IRequest<BrandFacePhotosDto>;

public sealed record RemoveBrandFacePhotoCommand(long TelegramUserId, string Url) : IRequest<BrandFacePhotosDto>;

public sealed class AddBrandFacePhotoHandler(
    IBrandFaceProfileRepository brandFaces,
    IProfileMediaStorage storage,
    IUnitOfWork unitOfWork,
    ILogger<AddBrandFacePhotoHandler> logger,
    ICatalogCache? cache = null) : IRequestHandler<AddBrandFacePhotoCommand, BrandFacePhotosDto>
{
    public async Task<BrandFacePhotosDto> Handle(AddBrandFacePhotoCommand command, CancellationToken cancellationToken)
    {
        var profile = await brandFaces.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken)
            ?? throw new InvalidOperationException("Profile not found.");
        if (!profile.CanAddPhoto)
        {
            throw new BusinessRuleConflictException("photo_limit", "The photo limit is reached.");
        }

        var uploaded = await storage.UploadAsync(new ProfileMediaUpload(ProfileMediaTarget.BrandFace, profile.Id, command.Content, command.FileName, command.ContentType), cancellationToken);
        try
        {
            profile.AddPhoto(uploaded.PublicUrl);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            try { await storage.DeleteAsync(uploaded.PublicUrl, cancellationToken); }
            catch (Exception exception) { logger.LogWarning(exception, "Brand face photo cleanup failed after a database write failure"); }
            throw;
        }

        if (cache is not null) await cache.RotateNamespaceVersionAsync(cancellationToken);
        return new BrandFacePhotosDto(profile.PhotoUrls);
    }
}

public sealed class RemoveBrandFacePhotoHandler(
    IBrandFaceProfileRepository brandFaces,
    IProfileMediaStorage storage,
    IUnitOfWork unitOfWork,
    ILogger<RemoveBrandFacePhotoHandler> logger,
    ICatalogCache? cache = null) : IRequestHandler<RemoveBrandFacePhotoCommand, BrandFacePhotosDto>
{
    public async Task<BrandFacePhotosDto> Handle(RemoveBrandFacePhotoCommand command, CancellationToken cancellationToken)
    {
        var profile = await brandFaces.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken)
            ?? throw new InvalidOperationException("Profile not found.");
        // Only a URL from the profile's own gallery is ever deleted from storage.
        if (!profile.RemovePhoto(command.Url))
        {
            return new BrandFacePhotosDto(profile.PhotoUrls);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        try { await storage.DeleteAsync(command.Url, cancellationToken); }
        catch (Exception exception) { logger.LogWarning(exception, "Brand face photo cleanup failed after removal"); }
        if (cache is not null) await cache.RotateNamespaceVersionAsync(cancellationToken);
        return new BrandFacePhotosDto(profile.PhotoUrls);
    }
}
