using BloggerBazar.Application.Abstractions.Persistence;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Reviews;

// D46: the same shape as business reviews: rating and count over all published reviews, one page of items.
public sealed record GetBrandFaceReviewsQuery(Guid BrandFaceId, int Take = 20, int Skip = 0) : IRequest<BusinessReviewsDto>;

public sealed class GetBrandFaceReviewsValidator : AbstractValidator<GetBrandFaceReviewsQuery>
{
    public GetBrandFaceReviewsValidator()
    {
        RuleFor(query => query.BrandFaceId).NotEmpty();
        RuleFor(query => query.Take).InclusiveBetween(1, 50);
        RuleFor(query => query.Skip).InclusiveBetween(0, 10_000);
    }
}

public sealed class GetBrandFaceReviewsHandler(IReviewReadModel reviews)
    : IRequestHandler<GetBrandFaceReviewsQuery, BusinessReviewsDto>
{
    public Task<BusinessReviewsDto> Handle(GetBrandFaceReviewsQuery query, CancellationToken cancellationToken) =>
        reviews.GetBrandFaceReviewsAsync(query.BrandFaceId, query.Skip, query.Take, cancellationToken);
}
