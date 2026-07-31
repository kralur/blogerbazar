using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Payments;

public sealed record GetUnlockedContactQuery(ContactTargetType TargetType, Guid TargetId) : IRequest<ContactDetailsDto>;

public sealed record ContactDetailsDto(string? Phone, string? Email, string? Telegram, string? WebsiteUrl);

public sealed class GetUnlockedContactValidator : AbstractValidator<GetUnlockedContactQuery>
{
    public GetUnlockedContactValidator()
    {
        RuleFor(query => query.TargetId).NotEmpty();
        RuleFor(query => query.TargetType).IsInEnum();
    }
}

public sealed class GetUnlockedContactHandler(
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses) : IRequestHandler<GetUnlockedContactQuery, ContactDetailsDto>
{
    public async Task<ContactDetailsDto> Handle(GetUnlockedContactQuery query, CancellationToken cancellationToken)
    {
        var contact = query.TargetType switch
        {
            ContactTargetType.Blogger => await GetBloggerContactAsync(query, cancellationToken),
            ContactTargetType.Business => await GetBusinessContactAsync(query, cancellationToken),
            _ => throw new InvalidOperationException("Contact target was not found.")
        };

        return contact;
    }

    private async Task<ContactDetailsDto> GetBloggerContactAsync(GetUnlockedContactQuery query, CancellationToken cancellationToken)
    {
        var target = await bloggers.GetByIdAsync(query.TargetId, cancellationToken) ?? throw new InvalidOperationException("Contact target was not found.");
        return new ContactDetailsDto(target.Phone, target.Email, target.Username, null);
    }

    private async Task<ContactDetailsDto> GetBusinessContactAsync(GetUnlockedContactQuery query, CancellationToken cancellationToken)
    {
        var target = await businesses.GetByIdAsync(query.TargetId, cancellationToken) ?? throw new InvalidOperationException("Contact target was not found.");
        return new ContactDetailsDto(target.Phone, target.Email, target.Username, target.WebsiteUrl);
    }
}
