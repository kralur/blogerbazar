using BloggerBazar.Application.Abstractions.Media;
using BloggerBazar.Application.Abstractions.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.ProfileMedia;

public sealed record ProfileMediaDto(string? Url);

public sealed record UploadProfileMediaCommand(
    long TelegramUserId,
    ProfileMediaTarget Target,
    ReadOnlyMemory<byte> Content,
    string FileName,
    string ContentType) : IRequest<ProfileMediaDto>;

public sealed class UploadProfileMediaHandler(
    IBloggerProfileRepository bloggers,
    IBrandFaceProfileRepository brandFaces,
    IBusinessProfileRepository businesses,
    IProfileMediaStorage storage,
    IUnitOfWork unitOfWork,
    ILogger<UploadProfileMediaHandler> logger) : IRequestHandler<UploadProfileMediaCommand, ProfileMediaDto>
{
    public async Task<ProfileMediaDto> Handle(UploadProfileMediaCommand command, CancellationToken cancellationToken)
    {
        var profile = await GetOwnedProfileAsync(command.TelegramUserId, command.Target, bloggers, brandFaces, businesses, cancellationToken);
        var uploaded = await storage.UploadAsync(new ProfileMediaUpload(command.Target, profile.Id, command.Content, command.FileName, command.ContentType), cancellationToken);

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                profile.SetPrimaryImageUrl(uploaded.PublicUrl);
                await unitOfWork.SaveChangesAsync(token);
                return 0;
            }, cancellationToken);
        }
        catch
        {
            await TryDeleteUploadedFileAsync(uploaded.PublicUrl, storage, logger, cancellationToken);
            throw;
        }

        await TryDeletePreviousFileAsync(profile.CurrentImageUrl, uploaded.PublicUrl, storage, logger, cancellationToken);
        return new ProfileMediaDto(uploaded.PublicUrl);
    }

    internal static async Task<OwnedProfileMedia> GetOwnedProfileAsync(
        long telegramUserId,
        ProfileMediaTarget target,
        IBloggerProfileRepository bloggers,
        IBrandFaceProfileRepository brandFaces,
        IBusinessProfileRepository businesses,
        CancellationToken cancellationToken)
    {
        return target switch
        {
            ProfileMediaTarget.Blogger => ToOwnedProfile(await bloggers.GetByTelegramUserIdAsync(telegramUserId, cancellationToken)),
            ProfileMediaTarget.BrandFace => ToOwnedProfile(await brandFaces.GetByTelegramUserIdAsync(telegramUserId, cancellationToken)),
            ProfileMediaTarget.Business => ToOwnedProfile(await businesses.GetByTelegramUserIdAsync(telegramUserId, cancellationToken)),
            _ => throw new InvalidOperationException("Profile not found.")
        };
    }

    private static OwnedProfileMedia ToOwnedProfile(BloggerBazar.Domain.Entities.BloggerProfile? profile) =>
        profile is null ? throw new InvalidOperationException("Profile not found.") : new(profile.Id, profile.AvatarUrl, profile.SetPrimaryImageUrl);

    private static OwnedProfileMedia ToOwnedProfile(BloggerBazar.Domain.Entities.BrandFaceProfile? profile) =>
        profile is null ? throw new InvalidOperationException("Profile not found.") : new(profile.Id, profile.AvatarUrl, profile.SetPrimaryImageUrl);

    private static OwnedProfileMedia ToOwnedProfile(BloggerBazar.Domain.Entities.BusinessProfile? profile) =>
        profile is null ? throw new InvalidOperationException("Profile not found.") : new(profile.Id, profile.LogoUrl, profile.SetPrimaryImageUrl);

    private static async Task TryDeleteUploadedFileAsync(string publicUrl, IProfileMediaStorage storage, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            await storage.DeleteAsync(publicUrl, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Profile media cleanup failed after a database write failure");
        }
    }

    private static async Task TryDeletePreviousFileAsync(string? previousUrl, string currentUrl, IProfileMediaStorage storage, ILogger logger, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(previousUrl) || string.Equals(previousUrl, currentUrl, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            await storage.DeleteAsync(previousUrl, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Previous profile media cleanup failed after replacement");
        }
    }

    internal sealed record OwnedProfileMedia(Guid Id, string? CurrentImageUrl, Action<string?> SetPrimaryImageUrl);
}

public sealed record DeleteProfileMediaCommand(long TelegramUserId, ProfileMediaTarget Target) : IRequest;

public sealed class DeleteProfileMediaHandler(
    IBloggerProfileRepository bloggers,
    IBrandFaceProfileRepository brandFaces,
    IBusinessProfileRepository businesses,
    IProfileMediaStorage storage,
    IUnitOfWork unitOfWork,
    ILogger<DeleteProfileMediaHandler> logger) : IRequestHandler<DeleteProfileMediaCommand>
{
    public async Task Handle(DeleteProfileMediaCommand command, CancellationToken cancellationToken)
    {
        var profile = await UploadProfileMediaHandler.GetOwnedProfileAsync(command.TelegramUserId, command.Target, bloggers, brandFaces, businesses, cancellationToken);
        if (string.IsNullOrWhiteSpace(profile.CurrentImageUrl))
        {
            return;
        }

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            profile.SetPrimaryImageUrl(null);
            await unitOfWork.SaveChangesAsync(token);
            return 0;
        }, cancellationToken);

        try
        {
            await storage.DeleteAsync(profile.CurrentImageUrl, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Profile media cleanup failed after deletion");
        }
    }
}
