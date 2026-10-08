using BloggerBazar.Application.Abstractions.Persistence;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Reviews;

public sealed record GetBloggerReviewsQuery(Guid BloggerId, int Take = 20, int Skip = 0) : IRequest<IReadOnlyList<ReviewDto>>;

public sealed class GetBloggerReviewsValidator : AbstractValidator<GetBloggerReviewsQuery>
{
    public GetBloggerReviewsValidator()
    {
        RuleFor(query => query.BloggerId).NotEmpty();
        RuleFor(query => query.Take).InclusiveBetween(1, 50);
        RuleFor(query => query.Skip).InclusiveBetween(0, 10_000);
    }
}

public sealed class GetBloggerReviewsHandler(IReviewReadModel reviews)
    : IRequestHandler<GetBloggerReviewsQuery, IReadOnlyList<ReviewDto>>
{
    public Task<IReadOnlyList<ReviewDto>> Handle(GetBloggerReviewsQuery query, CancellationToken cancellationToken) =>
        reviews.GetBloggerReviewsAsync(query.BloggerId, query.Skip, query.Take, cancellationToken);
}
