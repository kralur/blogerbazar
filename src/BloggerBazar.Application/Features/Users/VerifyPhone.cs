using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Validation;
using BloggerBazar.Domain.Entities;
using MediatR;

namespace BloggerBazar.Application.Features.Users;

// Telegram delivered a contact that the sender shared about themselves (contact.user_id == from.id),
// either from the Mini App's "share phone" request or the bot's /phone button (D41).
public sealed record VerifyTelegramPhoneCommand(long TelegramUserId, string FirstName, string? Username, string PhoneNumber) : IRequest<string?>;

public sealed class VerifyTelegramPhoneHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IUnitOfWork unitOfWork) : IRequestHandler<VerifyTelegramPhoneCommand, string?>
{
    public async Task<string?> Handle(VerifyTelegramPhoneCommand command, CancellationToken cancellationToken)
    {
        var phone = ContactValidation.NormalizeTelegramPhone(command.PhoneNumber);
        if (phone is null || command.TelegramUserId <= 0) return null;

        var user = await users.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken);
        if (user is { IsDeleted: true } or { IsBlocked: true }) return null;
        if (user is null)
        {
            // Someone may share the number from the bot chat before ever opening the Mini App.
            user = PlatformUser.Create(command.TelegramUserId, string.IsNullOrWhiteSpace(command.FirstName) ? "Telegram" : command.FirstName.Trim(), command.Username?.Trim().TrimStart('@'));
            await users.AddAsync(user, cancellationToken);
        }

        user.VerifyPhone(phone, DateTime.UtcNow);
        // Profiles that already exist switch to the confirmed number at once.
        (await bloggers.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken))?.SetVerifiedPhone(phone);
        (await businesses.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken))?.SetVerifiedPhone(phone);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return phone;
    }
}

public sealed record GetVerifiedPhoneQuery(long TelegramUserId) : IRequest<string?>;

public sealed class GetVerifiedPhoneHandler(IPlatformUserRepository users) : IRequestHandler<GetVerifiedPhoneQuery, string?>
{
    public async Task<string?> Handle(GetVerifiedPhoneQuery query, CancellationToken cancellationToken) =>
        (await users.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken))?.VerifiedPhone;
}
