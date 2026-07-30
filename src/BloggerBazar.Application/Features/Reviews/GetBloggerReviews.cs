using BloggerBazar.Application.Abstractions.Persistence;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Reviews;

public sealed record GetBloggerReviewsQuery(Guid BloggerId, int Take = 20) : IRequest<IReadOnlyList<ReviewDto>>;

public sealed class GetBloggerReviewsValidator : AbstractValidator<GetBloggerReviewsQuery>
{
    public GetBloggerReviewsValidator()
    {
        RuleFor(query => query.BloggerId).NotEmpty();
        RuleFor(query => query.Take).InclusiveBetween(1, 50);
    }
}

public sealed class GetBloggerReviewsHandler(IReviewReadModel reviews)
    : IRequestHandler<GetBloggerReviewsQuery, IReadOnlyList<ReviewDto>>
{
    public Task<IReadOnlyList<ReviewDto>> Handle(GetBloggerReviewsQuery query, CancellationToken cancellationToken) =>
        reviews.GetBloggerReviewsAsync(query.BloggerId, query.Take, cancellationToken);
}
