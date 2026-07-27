using BloggerBazar.Application.Features.Bloggers;

namespace BloggerBazar.Api.Contracts.Bloggers;

public sealed record SearchBloggersResponse(IReadOnlyList<BloggerProfileDto> Bloggers);
