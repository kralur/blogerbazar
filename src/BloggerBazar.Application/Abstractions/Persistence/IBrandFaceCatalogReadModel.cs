using BloggerBazar.Application.Features.BrandFaces;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IBrandFaceCatalogReadModel
{
    Task<BrandFaceCatalogResult> SearchAsync(BrandFaceCatalogSearch search, CancellationToken cancellationToken);
}
