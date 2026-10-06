using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Deals;

public sealed class DealReminderTests
{
    // 10:00 in Tashkent (UTC+5).
    private static readonly DateTime Daytime = new(2026, 10, 6, 5, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(3, 59, false)]  // 08:59 Tashkent
    [InlineData(4, 0, true)]    // 09:00
    [InlineData(15, 59, true)]  // 20:59
    [InlineData(16, 0, false)]  // 21:00
    [InlineData(22, 0, false)]  // 03:00 next day
    public void Messages_go_out_only_between_nine_and_twenty_one_in_tashkent(int utcHour, int minute, bool expected) =>
        Assert.Equal(expected, DealReminderSchedule.IsDeliveryTime(new DateTime(2026, 10, 6, utcHour, minute, 0, DateTimeKind.Utc)));

    [Theory]
    [InlineData(0.9, null)]
    [InlineData(1.0, DealReminderKind.ReviewDay1)]
    [InlineData(2.9, DealReminderKind.ReviewDay1)]
    [InlineData(3.0, DealReminderKind.ReviewDay3)]
    [InlineData(7.0, DealReminderKind.ReviewDay7)]
    [InlineData(13.9, DealReminderKind.ReviewDay7)]
    [InlineData(14.0, null)]
    public void Review_reminder_is_the_latest_due_stage_inside_the_window(double daysSinceCompletion, DealReminderKind? expected)
    {
        var deal = Completed(Daytime.AddDays(-daysSinceCompletion));

        var due = DealReminderSchedule.Due(deal, Daytime);

        if (expected is null)
        {
            Assert.Empty(due);
            return;
        }

        Assert.Equal([MarketplaceRole.Blogger, MarketplaceRole.Business], due.Select(reminder => reminder.RecipientRole));
        Assert.All(due, reminder => Assert.Equal(expected, reminder.Kind));
    }

    [Fact]
    public void Side_that_already_reviewed_gets_no_review_reminder()
    {
        var deal = Completed(Daytime.AddDays(-3)) with { BloggerHasReviewed = true };

        var reminder = Assert.Single(DealReminderSchedule.Due(deal, Daytime));

        Assert.Equal(MarketplaceRole.Business, reminder.RecipientRole);
        Assert.Equal(20, reminder.ChatId);
    }

    [Fact]
    public void Both_reviewed_means_no_reminders() =>
        Assert.Empty(DealReminderSchedule.Due(Completed(Daytime.AddDays(-3)) with { BloggerHasReviewed = true, BusinessHasReviewed = true }, Daytime));

    [Theory]
    [InlineData(6.9, null)]
    [InlineData(7.0, DealReminderKind.CompleteDay7)]
    [InlineData(14.0, DealReminderKind.CompleteDay14)]
    [InlineData(40.0, DealReminderKind.CompleteDay14)]
    public void Stuck_active_deal_reminds_both_sides_to_complete(double daysSinceStart, DealReminderKind? expected)
    {
        var deal = Active(Daytime.AddDays(-daysSinceStart));

        var due = DealReminderSchedule.Due(deal, Daytime);

        if (expected is null)
        {
            Assert.Empty(due);
            return;
        }

        Assert.Equal([10L, 20L], due.Select(reminder => reminder.ChatId));
        Assert.All(due, reminder => Assert.Equal(expected, reminder.Kind));
    }

    [Fact]
    public async Task Handler_sends_each_claimed_reminder_with_a_deal_button()
    {
        var deal = Completed(Daytime.AddDays(-1));
        var reminders = new FakeReminders(deal);
        var reviews = new SpyReviews();
        var bot = new SpyBotClient();
        var handler = new ProcessDealRemindersHandler(reminders, reviews, bot);

        var first = await handler.Handle(new ProcessDealRemindersCommand(Daytime), CancellationToken.None);
        var second = await handler.Handle(new ProcessDealRemindersCommand(Daytime.AddHours(1)), CancellationToken.None);

        Assert.Equal(2, first);
        Assert.Equal(0, second);
        Assert.Equal([10L, 20L], bot.NotifiedChats);
        Assert.All(bot.Routes, route => Assert.Equal($"/deal/{deal.DealId}", route));
        Assert.Equal([null, null], reviews.PublishedDeals);
    }

    [Fact]
    public async Task Night_run_publishes_reviews_but_sends_nothing()
    {
        var reminders = new FakeReminders(Completed(Daytime.AddDays(-1)));
        var reviews = new SpyReviews();
        var bot = new SpyBotClient();
        var night = new DateTime(2026, 10, 6, 20, 0, 0, DateTimeKind.Utc);

        var sent = await new ProcessDealRemindersHandler(reminders, reviews, bot).Handle(new ProcessDealRemindersCommand(night), CancellationToken.None);

        Assert.Equal(0, sent);
        Assert.Empty(bot.NotifiedChats);
        Assert.Empty(reminders.Claimed);
        Assert.Equal([null], reviews.PublishedDeals);
    }

    [Fact]
    public async Task Reminder_already_claimed_elsewhere_is_not_sent_again()
    {
        var deal = Active(Daytime.AddDays(-7));
        var reminders = new FakeReminders(deal);
        reminders.Claimed.Add((deal.DealId, DealReminderKind.CompleteDay7, MarketplaceRole.Blogger));
        var bot = new SpyBotClient();

        var sent = await new ProcessDealRemindersHandler(reminders, new SpyReviews(), bot).Handle(new ProcessDealRemindersCommand(Daytime), CancellationToken.None);

        Assert.Equal(1, sent);
        Assert.Equal([20L], bot.NotifiedChats);
        Assert.Contains("активна", Assert.Single(bot.Texts));
    }

    private static DealReminderCandidate Completed(DateTime completedAtUtc) =>
        new(Guid.NewGuid(), DealStatus.Completed, completedAtUtc.AddDays(-5), completedAtUtc, 10, 20, false, false);

    private static DealReminderCandidate Active(DateTime createdAtUtc) =>
        new(Guid.NewGuid(), DealStatus.Active, createdAtUtc, null, 10, 20, false, false);

    private sealed class FakeReminders(params DealReminderCandidate[] candidates) : IDealReminderRepository
    {
        public HashSet<(Guid DealId, DealReminderKind Kind, MarketplaceRole Role)> Claimed { get; } = [];

        public Task<IReadOnlyList<DealReminderCandidate>> GetCandidatesAsync(DateTime nowUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DealReminderCandidate>>(candidates);

        public Task<bool> TryClaimAsync(Guid dealId, DealReminderKind kind, MarketplaceRole recipientRole, DateTime nowUtc, CancellationToken cancellationToken) =>
            Task.FromResult(Claimed.Add((dealId, kind, recipientRole)));
    }

    private sealed class SpyReviews : IReviewRepository
    {
        public List<Guid?> PublishedDeals { get; } = [];
        public Task<bool> ExistsAsync(Guid dealId, long reviewerTelegramUserId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(Review review, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<int> PublishRevealedAsync(Guid? dealId, DateTime nowUtc, CancellationToken cancellationToken)
        {
            PublishedDeals.Add(dealId);
            return Task.FromResult(0);
        }
    }
}
