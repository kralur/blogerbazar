using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Payments;

public sealed record GetContactUnlockStatusQuery(long TelegramUserId, ContactTargetType TargetType, Guid TargetId) : IRequest<ContactUnlockStatusDto>;

public sealed record ContactUnlockStatusDto(bool IsUnlocked);

public sealed class GetContactUnlockStatusValidator : AbstractValidator<GetContactUnlockStatusQuery>
{
    public GetContactUnlockStatusValidator()
    {
        RuleFor(query => query.TelegramUserId).GreaterThan(0);
        RuleFor(query => query.TargetId).NotEmpty();
        RuleFor(query => query.TargetType).IsInEnum();
    }
}

public sealed class GetContactUnlockStatusHandler(
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IContactUnlockRepository contactUnlocks)
    : IRequestHandler<GetContactUnlockStatusQuery, ContactUnlockStatusDto>
{
    public async Task<ContactUnlockStatusDto> Handle(GetContactUnlockStatusQuery query, CancellationToken cancellationToken)
    {
        var targetTelegramUserId = query.TargetType switch
        {
            ContactTargetType.Blogger => (await bloggers.GetByIdAsync(query.TargetId, cancellationToken))?.TelegramUserId,
            ContactTargetType.Business => (await businesses.GetByIdAsync(query.TargetId, cancellationToken))?.TelegramUserId,
            _ => null
        } ?? throw new InvalidOperationException("Contact target was not found.");

        if (targetTelegramUserId == query.TelegramUserId)
        {
            return new ContactUnlockStatusDto(true);
        }

        return new ContactUnlockStatusDto(await contactUnlocks.GetAsync(query.TelegramUserId, query.TargetType, query.TargetId, cancellationToken) is not null);
    }
}
