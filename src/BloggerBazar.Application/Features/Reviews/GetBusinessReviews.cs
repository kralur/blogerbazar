using BloggerBazar.Application.Abstractions.Persistence;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Reviews;

public sealed record GetBusinessReviewsQuery(Guid BusinessId, int Take = 20) : IRequest<BusinessReviewsDto>;

// Rating and count cover all published reviews; Items holds only the latest Take of them.
public sealed record BusinessReviewsDto(decimal? Rating, int ReviewsCount, IReadOnlyList<ReviewDto> Items);

public sealed class GetBusinessReviewsValidator : AbstractValidator<GetBusinessReviewsQuery>
{
    public GetBusinessReviewsValidator()
    {
        RuleFor(query => query.BusinessId).NotEmpty();
        RuleFor(query => query.Take).InclusiveBetween(1, 50);
    }
}

public sealed class GetBusinessReviewsHandler(IReviewReadModel reviews)
    : IRequestHandler<GetBusinessReviewsQuery, BusinessReviewsDto>
{
    public Task<BusinessReviewsDto> Handle(GetBusinessReviewsQuery query, CancellationToken cancellationToken) =>
        reviews.GetBusinessReviewsAsync(query.BusinessId, query.Take, cancellationToken);
}
