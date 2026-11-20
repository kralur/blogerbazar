using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Social;
using BloggerBazar.Application.Features.Bloggers;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Bloggers;

// D67: Telegram / YouTube followers confirmed by the platform.
public sealed class PlatformVerificationTests
{
    private const long Owner = 4_100_001;

    [Theory]
    [InlineData("https://t.me/madina_blog", "madina_blog")]
    [InlineData("https://telegram.me/Madina_Blog/", "Madina_Blog")]
    [InlineData("https://t.me/+AbCdEf123", null)]
    [InlineData("https://t.me/joinchat/AbCd", null)]
    [InlineData("https://instagram.com/madina", null)]
    public void Reads_the_public_name_of_a_telegram_channel(string url, string? expected) =>
        Assert.Equal(expected, PlatformVerification.TelegramChannel(url));

    [Fact]
    public void Reads_a_youtube_handle_or_channel_id()
    {
        Assert.Equal(new YouTubeChannelRef("@madina.vlog", null), PlatformVerification.YouTubeChannel("https://www.youtube.com/@madina.vlog"));
        Assert.Equal(new YouTubeChannelRef(null, "UC1234567890abcdefghijkl"), PlatformVerification.YouTubeChannel("https://youtube.com/channel/UC1234567890abcdefghijkl"));
        Assert.Null(PlatformVerification.YouTubeChannel("https://youtube.com/c/oldname"));
    }

    [Fact]
    public void Gives_the_same_short_code_for_the_same_platform()
    {
        var id = Guid.NewGuid();
        Assert.Equal(PlatformVerification.CodeFor(id), PlatformVerification.CodeFor(id));
        Assert.Matches("^BB-[A-Z2-9]{6}$", PlatformVerification.CodeFor(id));
        Assert.NotEqual(PlatformVerification.CodeFor(id), PlatformVerification.CodeFor(Guid.NewGuid()));
    }

    [Fact]
    public async Task A_proved_telegram_channel_takes_its_real_followers_and_the_profile_total_follows()
    {
        var world = World();
        var telegram = world.Add("telegram", "https://t.me/madina_blog", 50_000);
        world.Add("instagram", "https://instagram.com/madina", 10_000);
        world.Client.Telegram = new PlatformCheck(PlatformCheckOutcomes.Verified, 12_345);

        var result = await world.Handler().Handle(new VerifyMyPlatformCommand(Owner, telegram.Id), CancellationToken.None);

        Assert.Equal(PlatformCheckOutcomes.Verified, result.Outcome);
        Assert.Equal("telegram", result.Platform.VerificationSource);
        Assert.Equal(12_345, telegram.Followers);
        Assert.Equal(22_345, world.Blogger.TotalFollowers);
        // Ownership is checked against the person pressing the button.
        Assert.Equal(Owner, world.Client.TelegramOwner);
    }

    [Theory]
    [InlineData(PlatformCheckOutcomes.NotOwner)]
    [InlineData(PlatformCheckOutcomes.BotNotAdmin)]
    [InlineData(PlatformCheckOutcomes.Unavailable)]
    public async Task Nothing_changes_when_the_channel_is_not_proved(string outcome)
    {
        var world = World();
        var telegram = world.Add("telegram", "https://t.me/madina_blog", 50_000);
        world.Client.Telegram = new PlatformCheck(outcome);

        var result = await world.Handler().Handle(new VerifyMyPlatformCommand(Owner, telegram.Id), CancellationToken.None);

        Assert.Equal(outcome, result.Outcome);
        Assert.False(telegram.IsVerified);
        Assert.Equal(50_000, telegram.Followers);
    }

    [Fact]
    public async Task A_youtube_channel_is_proved_by_its_code()
    {
        var world = World();
        var youTube = world.Add("youtube", "https://youtube.com/@madina", 1_000);
        world.Client.YouTube = new PlatformCheck(PlatformCheckOutcomes.CodeMissing);

        var missing = await world.Handler().Handle(new VerifyMyPlatformCommand(Owner, youTube.Id), CancellationToken.None);

        Assert.Equal(PlatformCheckOutcomes.CodeMissing, missing.Outcome);
        Assert.Equal(PlatformVerification.CodeFor(youTube.Id), missing.Code);
        Assert.Equal(PlatformVerification.CodeFor(youTube.Id), world.Client.YouTubeCode);
    }

    [Fact]
    public async Task Someone_elses_platform_is_missing_and_another_role_is_refused()
    {
        var world = World();
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.Handler().Handle(new VerifyMyPlatformCommand(Owner, Guid.NewGuid()), CancellationToken.None));
        world.User.SelectMarketplaceRole(MarketplaceRole.Business);
        var telegram = world.Add("telegram", "https://t.me/madina_blog", 1);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => world.Handler().Handle(new VerifyMyPlatformCommand(Owner, telegram.Id), CancellationToken.None));
    }

    [Fact]
    public async Task A_removed_bot_takes_the_mark_away_on_the_next_check()
    {
        var world = World();
        var telegram = world.Add("telegram", "https://t.me/madina_blog", 1);
        telegram.MarkVerified("telegram", 900, DateTime.UtcNow.AddDays(-1));
        world.Client.Telegram = new PlatformCheck(PlatformCheckOutcomes.BotNotAdmin);

        await world.Handler().Handle(new VerifyMyPlatformCommand(Owner, telegram.Id), CancellationToken.None);

        Assert.False(telegram.IsVerified);
        // A refresh only reads the numbers; ownership was proved before.
        Assert.Null(world.Client.TelegramOwner);
    }

    [Fact]
    public async Task Saving_the_profile_keeps_the_verification_of_the_same_channel()
    {
        var world = World();
        var telegram = world.Add("telegram", "https://t.me/madina_blog", 1);
        telegram.MarkVerified("telegram", 12_345, DateTime.UtcNow);
        var handler = new UpdateBloggerProfileHandler(world.Bloggers, new NoPortfolio(), world.Platforms, new NoUnitOfWork());

        await handler.Handle(new UpdateBloggerProfileCommand(
            Owner, "Madina", null, null, "tashkent", ["beauty"], null, null, 99_999, null, null, 300_000, 500_000, null, null, false, "+998901234567", null, [],
            Platforms: [new SocialPlatformInput("telegram", "https://t.me/madina_blog/", 99_999, null), new SocialPlatformInput("instagram", "https://instagram.com/madina", 1_000, null)]), CancellationToken.None);

        var kept = Assert.Single(world.Platforms.Items, item => item.Type == "telegram");
        Assert.True(kept.IsVerified);
        Assert.Equal(12_345, kept.Followers);
        Assert.Equal(13_345, world.Blogger.TotalFollowers);
    }

    private static TestWorld World()
    {
        var user = PlatformUser.Create(Owner, "Madina", null);
        user.SelectMarketplaceRole(MarketplaceRole.Blogger);
        var blogger = BloggerProfile.Create(Owner, "Madina", "tashkent", ["beauty"]);
        return new TestWorld(user, blogger);
    }

    private sealed class TestWorld(PlatformUser user, BloggerProfile blogger)
    {
        public PlatformUser User { get; } = user;
        public BloggerProfile Blogger { get; } = blogger;
        public FakeClient Client { get; } = new();
        public FakePlatforms Platforms { get; } = new();
        public FakeBloggers Bloggers => new(Blogger);

        public SocialPlatform Add(string type, string url, int followers)
        {
            var platform = SocialPlatform.Create(Blogger.Id, type, url, followers, null);
            Platforms.Items.Add(platform);
            return platform;
        }

        public VerifyMyPlatformHandler Handler() => new(new FakeUsers(User), Bloggers, Platforms, Client, new NoUnitOfWork());
    }

    private sealed class FakeClient : IPlatformStatsClient
    {
        public PlatformCheck Telegram { get; set; } = new(PlatformCheckOutcomes.Unavailable);
        public PlatformCheck YouTube { get; set; } = new(PlatformCheckOutcomes.Unavailable);
        public long? TelegramOwner { get; private set; }
        public string? YouTubeCode { get; private set; }

        public Task<PlatformCheck> CheckTelegramChannelAsync(string channelUsername, long? ownerTelegramUserId, CancellationToken cancellationToken)
        {
            TelegramOwner = ownerTelegramUserId;
            return Task.FromResult(Telegram);
        }

        public Task<PlatformCheck> CheckYouTubeChannelAsync(YouTubeChannelRef channel, string? requiredCode, CancellationToken cancellationToken)
        {
            YouTubeCode = requiredCode;
            return Task.FromResult(YouTube);
        }
    }

    private sealed class FakePlatforms : ISocialPlatformRepository
    {
        public List<SocialPlatform> Items { get; } = [];
        public Task DeleteForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken) { Items.RemoveAll(item => item.BloggerId == bloggerId); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<SocialPlatform> platforms, CancellationToken cancellationToken) { Items.AddRange(platforms); return Task.CompletedTask; }
        public Task<IReadOnlyList<SocialPlatform>> ListForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SocialPlatform>>(Items.Where(item => item.BloggerId == bloggerId).ToList());
        public Task<SocialPlatform?> GetForBloggerAsync(Guid bloggerId, Guid platformId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(item => item.BloggerId == bloggerId && item.Id == platformId));
    }

    private sealed class FakeUsers(PlatformUser user) : IPlatformUserRepository
    {
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<PlatformUser?>(telegramUserId == user.TelegramUserId ? user : null);
        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>([user]);
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(1);
        public Task AddAsync(PlatformUser value, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeBloggers(BloggerProfile profile) : IBloggerProfileRepository
    {
        public Task AddAsync(BloggerProfile value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(id == profile.Id ? profile : null);
        public Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(telegramUserId == profile.TelegramUserId ? profile : null);
        public Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
    }

    private sealed class NoPortfolio : IPortfolioItemRepository
    {
        public Task AddRangeAsync(IEnumerable<PortfolioItem> portfolioItems, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NoUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
