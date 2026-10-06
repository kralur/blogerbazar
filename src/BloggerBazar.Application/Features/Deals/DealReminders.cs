using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Features.Reviews;
using BloggerBazar.Application.Notifications;
using BloggerBazar.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.Deals;

public sealed record ProcessDealRemindersCommand(DateTime NowUtc) : IRequest<int>;

public sealed record DealReminderCandidate(
    Guid DealId,
    DealStatus Status,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    long BloggerTelegramUserId,
    long BusinessTelegramUserId,
    bool BloggerHasReviewed,
    bool BusinessHasReviewed);

public sealed record DueDealReminder(DealReminderKind Kind, MarketplaceRole RecipientRole, long ChatId);

public static class DealReminderSchedule
{
    // Tashkent has no daylight saving time.
    private static readonly TimeSpan TashkentOffset = TimeSpan.FromHours(5);
    private const int FirstDeliveryHour = 9;
    private const int LastDeliveryHour = 21;

    // Latest stage first: after downtime only the most recent due reminder is sent, earlier ones are skipped.
    private static readonly (int Days, DealReminderKind Kind)[] ReviewStages =
        [(7, DealReminderKind.ReviewDay7), (3, DealReminderKind.ReviewDay3), (1, DealReminderKind.ReviewDay1)];

    private static readonly (int Days, DealReminderKind Kind)[] CompletionStages =
        [(14, DealReminderKind.CompleteDay14), (7, DealReminderKind.CompleteDay7)];

    public static bool IsDeliveryTime(DateTime nowUtc)
    {
        var localHour = (nowUtc + TashkentOffset).Hour;
        return localHour >= FirstDeliveryHour && localHour < LastDeliveryHour;
    }

    public static IReadOnlyList<DueDealReminder> Due(DealReminderCandidate deal, DateTime nowUtc)
    {
        if (deal.Status == DealStatus.Active)
        {
            if (LatestDue(CompletionStages, deal.CreatedAtUtc, nowUtc) is not { } completionKind)
            {
                return [];
            }

            return
            [
                new DueDealReminder(completionKind, MarketplaceRole.Blogger, deal.BloggerTelegramUserId),
                new DueDealReminder(completionKind, MarketplaceRole.Business, deal.BusinessTelegramUserId)
            ];
        }

        if (deal.Status != DealStatus.Completed || deal.CompletedAtUtc is not { } completedAtUtc || !ReviewWindow.IsOpen(completedAtUtc, nowUtc))
        {
            return [];
        }

        var reviewKind = LatestDue(ReviewStages, completedAtUtc, nowUtc);
        if (reviewKind is null)
        {
            return [];
        }

        var due = new List<DueDealReminder>(2);
        if (!deal.BloggerHasReviewed) due.Add(new DueDealReminder(reviewKind.Value, MarketplaceRole.Blogger, deal.BloggerTelegramUserId));
        if (!deal.BusinessHasReviewed) due.Add(new DueDealReminder(reviewKind.Value, MarketplaceRole.Business, deal.BusinessTelegramUserId));
        return due;
    }

    private static DealReminderKind? LatestDue((int Days, DealReminderKind Kind)[] stages, DateTime startUtc, DateTime nowUtc)
    {
        foreach (var stage in stages)
        {
            if (startUtc.AddDays(stage.Days) <= nowUtc) return stage.Kind;
        }

        return null;
    }

    internal static string Text(DealReminderKind kind) => kind switch
    {
        DealReminderKind.ReviewDay7 => "BloggerBazar: осталась неделя, чтобы оценить завершённую сделку. Отзывы публикуются, когда обе стороны оценят друг друга.",
        DealReminderKind.ReviewDay1 or DealReminderKind.ReviewDay3 => "BloggerBazar: оцените завершённую сделку. Отзывы публикуются, когда обе стороны оценят друг друга.",
        _ => "BloggerBazar: сделка всё ещё активна. Если сотрудничество завершено, отметьте это в приложении."
    };
}

public sealed class ProcessDealRemindersHandler(
    IDealReminderRepository reminders,
    IReviewRepository reviews,
    ITelegramBotClient? botClient = null,
    ILogger<ProcessDealRemindersHandler>? logger = null) : IRequestHandler<ProcessDealRemindersCommand, int>
{
    public async Task<int> Handle(ProcessDealRemindersCommand command, CancellationToken cancellationToken)
    {
        // Publishing hidden reviews is not a message, so it runs regardless of the delivery hours.
        await reviews.PublishRevealedAsync(null, command.NowUtc, cancellationToken);
        if (!DealReminderSchedule.IsDeliveryTime(command.NowUtc))
        {
            return 0;
        }

        var sent = 0;
        foreach (var deal in await reminders.GetCandidatesAsync(command.NowUtc, cancellationToken))
        {
            foreach (var reminder in DealReminderSchedule.Due(deal, command.NowUtc))
            {
                if (!await reminders.TryClaimAsync(deal.DealId, reminder.Kind, reminder.RecipientRole, command.NowUtc, cancellationToken))
                {
                    continue;
                }

                await BestEffortTelegramNotification.SendAsync(botClient, logger, reminder.ChatId, DealReminderSchedule.Text(reminder.Kind), $"/deal/{deal.DealId}", cancellationToken);
                sent++;
            }
        }

        return sent;
    }
}
