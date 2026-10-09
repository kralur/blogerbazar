using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Deals;

// The partner's phone as a Telegram contact card in the person's own chat with the bot: Telegram blocks calls
// from a Mini App on iPhone, while a contact card has its own Call and Add to contacts buttons.
public sealed record ShareDealContactCommand(Guid DealId, long TelegramUserId) : IRequest<Unit>;

public sealed class ShareDealContactValidator : AbstractValidator<ShareDealContactCommand>
{
    public ShareDealContactValidator()
    {
        RuleFor(command => command.DealId).NotEmpty();
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
    }
}

public sealed class ShareDealContactHandler(
    IDealRepository deals,
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    ITelegramBotClient botClient) : IRequestHandler<ShareDealContactCommand, Unit>
{
    public async Task<Unit> Handle(ShareDealContactCommand command, CancellationToken cancellationToken)
    {
        // Same access as the contacts block: the selected role's own deal, both sides still existing.
        var participant = await DealAccess.FindDealParticipantAsync(users, bloggers, businesses, command.TelegramUserId, cancellationToken)
            ?? throw DealAccess.DealNotFound();
        var deal = await deals.GetForParticipantAsync(command.DealId, participant.Role, participant.ProfileId, cancellationToken)
            ?? throw DealAccess.DealNotFound();
        var (name, phone) = participant.Role == MarketplaceRole.Blogger
            ? (deal.Business.Name, deal.Business.Phone)
            : (deal.Blogger.Name, deal.Blogger.Phone);
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new BusinessRuleConflictException("contact_phone_missing", "The partner has no phone number.");
        }

        try
        {
            await botClient.SendContactAsync(command.TelegramUserId, phone, name, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Usually the person has never started the bot or has blocked it.
            throw new BusinessRuleConflictException("contact_share_failed", "The contact could not be sent to the bot chat.");
        }

        return Unit.Value;
    }
}
