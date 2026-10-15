using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Users;
using BloggerBazar.Application.Tests.Features.Deals;
using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Tests.Features.Users;

// Analytics counters: the ad label an account first came with, and the last app open.
public sealed class AccountActivityTests
{
    [Theory]
    [InlineData("Insta", "insta")]
    [InlineData("blogger_madina", "blogger_madina")]
    [InlineData("bad label", null)]
    [InlineData("<script>", null)]
    [InlineData("", null)]
    public async Task A_new_account_keeps_a_valid_ad_label(string source, string? expected)
    {
        var users = new Users();

        await new GetCurrentPlatformUserHandler(users, new SpyUnitOfWork()).Handle(new GetCurrentPlatformUserCommand(5, "Ali", null, source), CancellationToken.None);

        Assert.Equal(expected, users.Single.Source);
        Assert.NotNull(users.Single.LastSeenAtUtc);
    }

    [Fact]
    public async Task A_later_link_does_not_change_where_the_account_came_from()
    {
        var users = new Users();
        var handler = new GetCurrentPlatformUserHandler(users, new SpyUnitOfWork());
        await handler.Handle(new GetCurrentPlatformUserCommand(5, "Ali", null, "insta"), CancellationToken.None);

        await handler.Handle(new GetCurrentPlatformUserCommand(5, "Ali", null, "tiktok"), CancellationToken.None);

        Assert.Equal("insta", users.Single.Source);
    }

    [Fact]
    public void Last_open_is_written_at_most_once_an_hour()
    {
        var user = PlatformUser.Create(5, "Ali", null);
        var first = new DateTime(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc);

        user.MarkSeen(first);
        user.MarkSeen(first.AddMinutes(59));
        Assert.Equal(first, user.LastSeenAtUtc);

        user.MarkSeen(first.AddMinutes(61));
        Assert.Equal(first.AddMinutes(61), user.LastSeenAtUtc);
    }

    private sealed class Users : IPlatformUserRepository
    {
        private readonly List<PlatformUser> items = [];
        public PlatformUser Single => Assert.Single(items);
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(items.SingleOrDefault(user => user.TelegramUserId == telegramUserId));
        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>(items);
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(items.Count);
        public Task AddAsync(PlatformUser user, CancellationToken cancellationToken) { items.Add(user); return Task.CompletedTask; }
    }
}
