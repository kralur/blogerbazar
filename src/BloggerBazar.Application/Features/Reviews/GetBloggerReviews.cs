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

public sealed class GetBloggerReviewsHandler(IReviewRepository reviews)
    : IRequestHandler<GetBloggerReviewsQuery, IReadOnlyList<ReviewDto>>
{
    public async Task<IReadOnlyList<ReviewDto>> Handle(GetBloggerReviewsQuery query, CancellationToken cancellationToken) =>
        (await reviews.GetForBloggerAsync(query.BloggerId, query.Take, cancellationToken)).Select(ReviewDto.From).ToArray();
}
