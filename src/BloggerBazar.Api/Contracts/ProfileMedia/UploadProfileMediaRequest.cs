using Microsoft.AspNetCore.Http;

namespace BloggerBazar.Api.Contracts.ProfileMedia;

public sealed class UploadProfileMediaRequest
{
    public IFormFile? File { get; init; }
}
