using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Notifications;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.Deals;

public sealed record CompleteDealCommand(Guid DealId, long TelegramUserId) : IRequest<DealDto>;

public sealed class CompleteDealValidator : AbstractValidator<CompleteDealCommand>
{
    public CompleteDealValidator()
    {
        RuleFor(command => command.DealId).NotEmpty();
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
    }
}

public sealed class CompleteDealHandler(
    IDealRepository deals,
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    ITelegramBotClient? botClient = null,
    ILogger<CompleteDealHandler>? logger = null)
    : IRequestHandler<CompleteDealCommand, DealDto>
{
    public async Task<DealDto> Handle(CompleteDealCommand command, CancellationToken cancellationToken)
    {
        var participant = await DealAccess.FindDealParticipantAsync(users, bloggers, businesses, command.TelegramUserId, cancellationToken)
            ?? throw DealAccess.DealNotFound();
        var deal = await deals.GetForParticipantAsync(command.DealId, participant.Role, participant.ProfileId, cancellationToken)
            ?? throw DealAccess.DealNotFound();

        if (deal.Status == DealStatus.Active)
        {
            var completedByThisRequest = await deals.TryCompleteAsync(deal.Id, DateTime.UtcNow, cancellationToken);
            deal = await deals.GetForParticipantAsync(command.DealId, participant.Role, participant.ProfileId, cancellationToken)
                ?? throw DealAccess.DealNotFound();

            if (completedByThisRequest && deal.Status == DealStatus.Completed)
            {
                var targetChatId = participant.Role == MarketplaceRole.Blogger ? deal.Business.TelegramUserId : deal.Blogger.TelegramUserId;
                await BestEffortTelegramNotification.SendAsync(botClient, logger, targetChatId, "BloggerBazar: сделка завершена. Теперь можно оставить отзыв.", cancellationToken);
            }
        }

        if (deal.Status != DealStatus.Completed)
        {
            throw new InvalidOperationException("Only active deals can be completed.");
        }

        return DealDto.From(deal);
    }
}
