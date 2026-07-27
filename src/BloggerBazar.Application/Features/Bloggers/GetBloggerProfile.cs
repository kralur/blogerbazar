using BloggerBazar.Application.Abstractions.Persistence;
using MediatR;

namespace BloggerBazar.Application.Features.Bloggers;

public sealed record GetBloggerProfileQuery(Guid Id) : IRequest<BloggerProfileDto?>;

public sealed class GetBloggerProfileHandler(IBloggerProfileRepository profiles)
    : IRequestHandler<GetBloggerProfileQuery, BloggerProfileDto?>
{
    public async Task<BloggerProfileDto?> Handle(GetBloggerProfileQuery query, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetByIdAsync(query.Id, cancellationToken);
        return profile is null ? null : BloggerProfileDto.From(profile);
    }
}
