using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Application.Features.Tips;
using BloggerBazar.Application.Tests.Features.Deals;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Tips;

public sealed class BotTipTests
{
    // Monday 12 October 2026, 12:00 in Tashkent (UTC+5).
    private static readonly DateTime MondayNoon = new(2026, 10, 12, 7, 0, 0, DateTimeKind.Utc);

    // Routes the Mini App knows (App.tsx knownRoutes), so every tip button opens a real screen.
    private static readonly HashSet<string> KnownRoutes = ["/", "/profile", "/settings", "/favorites", "/blogger-form", "/business", "/brand-face", "/search", "/campaigns", "/my-campaigns", "/requests"];

    [Fact]
    public void Catalog_has_unique_numbers_both_languages_and_known_routes()
    {
        Assert.True(BotTipCatalog.All.Count >= 30);
        Assert.Equal(BotTipCatalog.All.Count, BotTipCatalog.All.Select(tip => tip.Number).Distinct().Count());
        Assert.All(BotTipCatalog.All, tip =>
        {
            Assert.True(tip.Number > 0);
            Assert.Contains(tip.Route, KnownRoutes);
            Assert.False(string.IsNullOrWhiteSpace(tip.Text.Russian));
            Assert.False(string.IsNullOrWhiteSpace(tip.Text.Uzbek));
            Assert.False(string.IsNullOrWhiteSpace(tip.Button.Russian));
            Assert.False(string.IsNullOrWhiteSpace(tip.Button.Uzbek));
            // Owner's rule: no em dash in user-facing texts.
            Assert.DoesNotContain('—', tip.Text.Russian + tip.Text.Uzbek + tip.Button.Russian + tip.Button.Uzbek);
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData(MarketplaceRole.Blogger)]
    [InlineData(MarketplaceRole.BrandFace)]
    [InlineData(MarketplaceRole.Business)]
    public void Each_role_walks_through_its_own_tips_without_repeats_then_starts_over(MarketplaceRole? role)
    {
        var expected = BotTipCatalog.All.Where(tip => tip.IsFor(role)).Select(tip => tip.Number).Order().ToArray();
        Assert.True(expected.Length >= 10);

        var sent = new List<int>();
        int? last = null;
        for (var i = 0; i < expected.Length; i++)
        {
            var tip = BotTipCatalog.Next(role, last)!;
            sent.Add(tip.Number);
            last = tip.Number;
        }

        Assert.Equal(expected, sent);
        Assert.Equal(expected[0], BotTipCatalog.Next(role, last)!.Number);
    }

    [Fact]
    public void Business_never_gets_creator_tips_and_creators_never_get_business_tips()
    {
        Assert.DoesNotContain(BotTipCatalog.All.Where(tip => tip.IsFor(MarketplaceRole.Business)), tip => tip.Audience is TipAudience.Creator or TipAudience.Blogger or TipAudience.BrandFace);
        Assert.DoesNotContain(BotTipCatalog.All.Where(tip => tip.IsFor(MarketplaceRole.Blogger)), tip => tip.Audience is TipAudience.Business or TipAudience.BrandFace);
    }

    [Theory]
    [InlineData(2026, 10, 12, 5, 59, false)]  // Monday 10:59 Tashkent
    [InlineData(2026, 10, 12, 6, 0, true)]    // Monday 11:00
    [InlineData(2026, 10, 12, 14, 59, true)]  // Monday 19:59
    [InlineData(2026, 10, 12, 15, 0, false)]  // Monday 20:00
    [InlineData(2026, 10, 13, 7, 0, false)]   // Tuesday
    [InlineData(2026, 10, 14, 7, 0, true)]    // Wednesday
    [InlineData(2026, 10, 16, 7, 0, true)]    // Friday
    [InlineData(2026, 10, 18, 7, 0, false)]   // Sunday
    public void Tips_go_out_on_monday_wednesday_and_friday_daytime(int year, int month, int day, int utcHour, int minute, bool expected) =>
        Assert.Equal(expected, BotTipSchedule.IsTipTime(new DateTime(year, month, day, utcHour, minute, 0, DateTimeKind.Utc)));

    [Fact]
    public void Day_starts_at_tashkent_midnight() =>
        Assert.Equal(new DateTime(2026, 10, 11, 19, 0, 0, DateTimeKind.Utc), BotTipSchedule.DayStartUtc(MondayNoon));

    [Fact]
    public async Task Handler_sends_the_next_tip_once_per_day_with_its_screen_button()
    {
        var recipients = new FakeTipRecipients(
            new BotTipRecipient(10, MarketplaceRole.Business, null),
            new BotTipRecipient(20, MarketplaceRole.Blogger, 3));
        var bot = new SpyBotClient();
        var handler = new ProcessBotTipsHandler(recipients, bot, pauseBetweenMessages: TimeSpan.Zero);

        var first = await handler.Handle(new ProcessBotTipsCommand(MondayNoon), CancellationToken.None);
        var second = await handler.Handle(new ProcessBotTipsCommand(MondayNoon.AddHours(1)), CancellationToken.None);

        Assert.Equal(2, first);
        Assert.Equal(0, second);
        Assert.Equal([10L, 20L], bot.NotifiedChats);
        var businessTip = BotTipCatalog.Next(MarketplaceRole.Business, null)!;
        var bloggerTip = BotTipCatalog.Next(MarketplaceRole.Blogger, 3)!;
        Assert.Equal([businessTip.Route, bloggerTip.Route], bot.Routes);
        Assert.Equal(businessTip.Number, recipients.LastNumbers[10]);
        Assert.Equal(bloggerTip.Number, recipients.LastNumbers[20]);
        Assert.All(bot.Texts, text => Assert.Contains("Профиль → Настройки", text));
    }

    [Fact]
    public async Task Nothing_is_sent_outside_tip_days()
    {
        var recipients = new FakeTipRecipients(new BotTipRecipient(10, null, null));
        var bot = new SpyBotClient();

        var sent = await new ProcessBotTipsHandler(recipients, bot, pauseBetweenMessages: TimeSpan.Zero)
            .Handle(new ProcessBotTipsCommand(MondayNoon.AddDays(1)), CancellationToken.None);

        Assert.Equal(0, sent);
        Assert.Empty(bot.NotifiedChats);
        Assert.Empty(recipients.LastNumbers);
    }

    [Fact]
    public async Task Tip_claimed_by_another_instance_is_not_sent_again()
    {
        var recipients = new FakeTipRecipients(new BotTipRecipient(10, null, null)) { ClaimedElsewhere = { 10 } };
        var bot = new SpyBotClient();

        var sent = await new ProcessBotTipsHandler(recipients, bot, pauseBetweenMessages: TimeSpan.Zero)
            .Handle(new ProcessBotTipsCommand(MondayNoon), CancellationToken.None);

        Assert.Equal(0, sent);
        Assert.Empty(bot.NotifiedChats);
    }

    [Fact]
    public async Task Admin_preview_sends_the_next_tip_each_time_without_marking_the_day()
    {
        var owner = PlatformUser.Create(7, "Umid", null);
        owner.SelectMarketplaceRole(MarketplaceRole.Business);
        var bot = new SpyBotClient();
        var handler = new SendBotTipPreviewHandler(new FakeUsers(owner), new AllowAdmin(), new SpyUnitOfWork(), bot);

        var first = await handler.Handle(new SendBotTipPreviewCommand(7), CancellationToken.None);
        var second = await handler.Handle(new SendBotTipPreviewCommand(7), CancellationToken.None);

        Assert.Equal(BotTipCatalog.Next(MarketplaceRole.Business, null)!.Number, first.Number);
        Assert.Equal(BotTipCatalog.Next(MarketplaceRole.Business, first.Number)!.Number, second.Number);
        Assert.True(first.Sent);
        Assert.Equal(BotTipCatalog.All.Count(tip => tip.IsFor(MarketplaceRole.Business)), first.Total);
        Assert.Equal([7L, 7L], bot.NotifiedChats);
        Assert.Null(owner.LastTipSentAtUtc);
    }

    [Fact]
    public async Task Non_admin_cannot_preview_tips()
    {
        var bot = new SpyBotClient();
        var handler = new SendBotTipPreviewHandler(new FakeUsers(PlatformUser.Create(8, "Ali", null)), new DenyAdmin(), new SpyUnitOfWork(), bot);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new SendBotTipPreviewCommand(8), CancellationToken.None));
        Assert.Empty(bot.NotifiedChats);
    }

    private sealed class AllowAdmin : IAdminAccessPolicy
    {
        public void EnsureAllowed(long telegramUserId) { }
    }

    private sealed class DenyAdmin : IAdminAccessPolicy
    {
        public void EnsureAllowed(long telegramUserId) => throw new UnauthorizedAccessException("Admin access is required.");
    }

    // Mirrors the repository: claimed users drop out of the due list for the day.
    private sealed class FakeTipRecipients(params BotTipRecipient[] users) : IBotTipRepository
    {
        public Dictionary<long, int?> LastNumbers { get; } = [];
        public HashSet<long> ClaimedElsewhere { get; } = [];

        public Task<IReadOnlyList<BotTipRecipient>> GetDueAsync(DateTime dayStartUtc, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BotTipRecipient>>(users
                .Where(user => !LastNumbers.ContainsKey(user.TelegramUserId) && !ClaimedElsewhere.Contains(user.TelegramUserId))
                .Take(take)
                .ToArray());

        public Task<bool> TryClaimAsync(long telegramUserId, int? tipNumber, DateTime dayStartUtc, DateTime nowUtc, CancellationToken cancellationToken)
        {
            if (ClaimedElsewhere.Contains(telegramUserId) || LastNumbers.ContainsKey(telegramUserId))
            {
                return Task.FromResult(false);
            }

            LastNumbers[telegramUserId] = tipNumber;
            return Task.FromResult(true);
        }
    }
}
