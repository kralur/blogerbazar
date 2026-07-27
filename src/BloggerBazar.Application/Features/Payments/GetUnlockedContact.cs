using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Payments;

public sealed record GetUnlockedContactQuery(long TelegramUserId, ContactTargetType TargetType, Guid TargetId) : IRequest<ContactDetailsDto>;

public sealed record ContactDetailsDto(string? Phone, string? Email);

public sealed class GetUnlockedContactValidator : AbstractValidator<GetUnlockedContactQuery>
{
    public GetUnlockedContactValidator()
    {
        RuleFor(query => query.TelegramUserId).GreaterThan(0);
        RuleFor(query => query.TargetId).NotEmpty();
        RuleFor(query => query.TargetType).IsInEnum();
    }
}

public sealed class GetUnlockedContactHandler(
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IContactUnlockRepository contactUnlocks) : IRequestHandler<GetUnlockedContactQuery, ContactDetailsDto>
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
        await EnsureAccessAsync(query, target.TelegramUserId, cancellationToken);
        return new ContactDetailsDto(target.Phone, target.Email);
    }

    private async Task<ContactDetailsDto> GetBusinessContactAsync(GetUnlockedContactQuery query, CancellationToken cancellationToken)
    {
        var target = await businesses.GetByIdAsync(query.TargetId, cancellationToken) ?? throw new InvalidOperationException("Contact target was not found.");
        await EnsureAccessAsync(query, target.TelegramUserId, cancellationToken);
        return new ContactDetailsDto(target.Phone, target.Email);
    }

    private async Task EnsureAccessAsync(GetUnlockedContactQuery query, long targetTelegramUserId, CancellationToken cancellationToken)
    {
        if (targetTelegramUserId == query.TelegramUserId)
        {
            return;
        }

        if (await contactUnlocks.GetAsync(query.TelegramUserId, query.TargetType, query.TargetId, cancellationToken) is null)
        {
            throw new UnauthorizedAccessException("Contact access must be unlocked first.");
        }
    }
}
