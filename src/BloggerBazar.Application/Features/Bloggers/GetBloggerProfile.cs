using BloggerBazar.Application.Abstractions.Persistence;
using MediatR;

namespace BloggerBazar.Application.Features.Bloggers;

public sealed record GetBloggerProfileQuery(Guid Id) : IRequest<BloggerProfileDto?>;

public sealed class GetBloggerProfileHandler(IMarketplaceCatalogReadModel catalog)
    : IRequestHandler<GetBloggerProfileQuery, BloggerProfileDto?>
{
    public async Task<BloggerProfileDto?> Handle(GetBloggerProfileQuery query, CancellationToken cancellationToken)
    {
        return await catalog.GetBloggerAsync(query.Id, cancellationToken);
    }
}
