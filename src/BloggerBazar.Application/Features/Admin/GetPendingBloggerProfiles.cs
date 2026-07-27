using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Admin;

public sealed record GetPendingBloggerProfilesQuery(long TelegramUserId, int Take = 50) : IRequest<IReadOnlyList<AdminBloggerProfileDto>>;

public sealed class GetPendingBloggerProfilesValidator : AbstractValidator<GetPendingBloggerProfilesQuery>
{
    public GetPendingBloggerProfilesValidator()
    {
        RuleFor(query => query.TelegramUserId).GreaterThan(0);
        RuleFor(query => query.Take).InclusiveBetween(1, 100);
    }
}

public sealed class GetPendingBloggerProfilesHandler(IBloggerProfileRepository profiles, IAdminAccessPolicy adminAccess)
    : IRequestHandler<GetPendingBloggerProfilesQuery, IReadOnlyList<AdminBloggerProfileDto>>
{
    public async Task<IReadOnlyList<AdminBloggerProfileDto>> Handle(GetPendingBloggerProfilesQuery query, CancellationToken cancellationToken)
    {
        adminAccess.EnsureAllowed(query.TelegramUserId);
        return (await profiles.GetPendingAsync(query.Take, cancellationToken)).Select(AdminBloggerProfileDto.From).ToArray();
    }
}
