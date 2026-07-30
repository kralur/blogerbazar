using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Notifications;
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

public sealed class CompleteDealHandler(IDealRepository deals, IBloggerProfileRepository bloggers, IBusinessProfileRepository businesses, IUnitOfWork unitOfWork, ITelegramBotClient? botClient = null, ILogger<CompleteDealHandler>? logger = null)
    : IRequestHandler<CompleteDealCommand, DealDto>
{
    public async Task<DealDto> Handle(CompleteDealCommand command, CancellationToken cancellationToken)
    {
        var deal = await deals.GetByIdAsync(command.DealId, cancellationToken) ?? throw new InvalidOperationException("Deal was not found.");
        var blogger = await bloggers.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken);
        var business = await businesses.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken);
        if (blogger?.Id != deal.BloggerId && business?.Id != deal.BusinessId)
        {
            throw new UnauthorizedAccessException("You are not a participant in this deal.");
        }

        deal.Complete();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var targetChatId = blogger?.Id == deal.BloggerId ? (await businesses.GetByIdAsync(deal.BusinessId, cancellationToken))?.TelegramUserId : (await bloggers.GetByIdAsync(deal.BloggerId, cancellationToken))?.TelegramUserId;
        if (targetChatId.HasValue) await BestEffortTelegramNotification.SendAsync(botClient, logger, targetChatId.Value, "BloggerBazar: сделка завершена. Теперь можно оставить отзыв.", cancellationToken);
        return DealDto.From(deal);
    }
}
