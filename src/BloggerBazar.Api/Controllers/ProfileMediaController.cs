using BloggerBazar.Application.Features.BrandFaces;
using BloggerBazar.Api.Contracts.ProfileMedia;
using BloggerBazar.Application.Abstractions.Media;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Features.ProfileMedia;
using BloggerBazar.Infrastructure.Configuration;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/profile-media")]
public sealed class ProfileMediaController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpPut("{target}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProfileMediaOptions.DefaultMaxFileSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ProfileMediaOptions.DefaultMaxFileSizeBytes)]
    [ProducesResponseType<ProfileMediaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ProfileMediaDto>> Upload(
        string target,
        [FromForm] UploadProfileMediaRequest request,
        CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var file = request.File;
        if (file is null || file.Length == 0 || file.Length > ProfileMediaOptions.DefaultMaxFileSizeBytes)
        {
            throw new ProfileMediaValidationException();
        }

        await using var content = new MemoryStream((int)file.Length);
        await file.CopyToAsync(content, cancellationToken);
        var result = await sender.Send(new UploadProfileMediaCommand(
            actor.Id,
            ParseTarget(target),
            content.ToArray(),
            file.FileName,
            file.ContentType), cancellationToken);
        return Ok(result);
    }

    // The brand face gallery besides the avatar (QA Q20): up to four photos, same limits and processing as the avatar.
    [HttpPost("brand-face/photos")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProfileMediaOptions.DefaultMaxFileSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ProfileMediaOptions.DefaultMaxFileSizeBytes)]
    [ProducesResponseType<BrandFacePhotosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BrandFacePhotosDto>> AddBrandFacePhoto([FromForm] UploadProfileMediaRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var file = request.File;
        if (file is null || file.Length == 0 || file.Length > ProfileMediaOptions.DefaultMaxFileSizeBytes)
        {
            throw new ProfileMediaValidationException();
        }

        await using var content = new MemoryStream((int)file.Length);
        await file.CopyToAsync(content, cancellationToken);
        return Ok(await sender.Send(new AddBrandFacePhotoCommand(actor.Id, content.ToArray(), file.FileName, file.ContentType), cancellationToken));
    }

    [HttpDelete("brand-face/photos")]
    [ProducesResponseType<BrandFacePhotosDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BrandFacePhotosDto>> RemoveBrandFacePhoto([FromQuery] string url, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new RemoveBrandFacePhotoCommand(actor.Id, url ?? string.Empty), cancellationToken));
    }

    [HttpDelete("{target}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string target, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        await sender.Send(new DeleteProfileMediaCommand(actor.Id, ParseTarget(target)), cancellationToken);
        return NoContent();
    }

    private static ProfileMediaTarget ParseTarget(string target) => target.ToLowerInvariant() switch
    {
        "blogger" => ProfileMediaTarget.Blogger,
        "brand-face" => ProfileMediaTarget.BrandFace,
        "business" => ProfileMediaTarget.Business,
        _ => throw new InvalidOperationException("Profile not found.")
    };
}
