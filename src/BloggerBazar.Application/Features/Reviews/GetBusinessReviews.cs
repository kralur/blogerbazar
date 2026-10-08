using BloggerBazar.Application.Abstractions.Persistence;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Reviews;

public sealed record GetBusinessReviewsQuery(Guid BusinessId, int Take = 20, int Skip = 0) : IRequest<BusinessReviewsDto>;

// Rating and count cover all published reviews; Items holds one page of them, newest first.
public sealed record BusinessReviewsDto(decimal? Rating, int ReviewsCount, IReadOnlyList<ReviewDto> Items);

public sealed class GetBusinessReviewsValidator : AbstractValidator<GetBusinessReviewsQuery>
{
    public GetBusinessReviewsValidator()
    {
        RuleFor(query => query.BusinessId).NotEmpty();
        RuleFor(query => query.Take).InclusiveBetween(1, 50);
        RuleFor(query => query.Skip).InclusiveBetween(0, 10_000);
    }
}

public sealed class GetBusinessReviewsHandler(IReviewReadModel reviews)
    : IRequestHandler<GetBusinessReviewsQuery, BusinessReviewsDto>
{
    public Task<BusinessReviewsDto> Handle(GetBusinessReviewsQuery query, CancellationToken cancellationToken) =>
        reviews.GetBusinessReviewsAsync(query.BusinessId, query.Skip, query.Take, cancellationToken);
}
