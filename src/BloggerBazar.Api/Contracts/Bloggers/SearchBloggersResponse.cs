using BloggerBazar.Application.Features.Bloggers;

namespace BloggerBazar.Api.Contracts.Bloggers;

public sealed record SearchBloggersResponse(IReadOnlyList<BloggerProfileDto> Bloggers, int Total, int Page, int PageSize)
{
    public static SearchBloggersResponse From(SearchBloggersResult result) => new(result.Bloggers, result.Total, result.Page, result.PageSize);
}
