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

    // Found by a load test: two first requests of one account in the same instant made the second one fail on the
    // unique index with a 500. It now reads the account the other request created and continues as a returning one.
    [Fact]
    public async Task A_parallel_first_request_continues_with_the_account_that_won()
    {
        var winner = PlatformUser.Create(5, "Ali", "ali");
        var users = new RacingUsers(winner);
        var unitOfWork = new RacingUnitOfWork();

        var result = await new GetCurrentPlatformUserHandler(users, unitOfWork).Handle(new GetCurrentPlatformUserCommand(5, "Aliya", null, "insta"), CancellationToken.None);

        Assert.Equal("Aliya", result.FirstName);
        Assert.Equal("Aliya", winner.FirstName);
        Assert.NotNull(winner.LastSeenAtUtc);
        Assert.Equal(1, unitOfWork.TryCalls);
        Assert.Equal(1, unitOfWork.SaveCalls);
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

    // The account is missing on the first look and present on the second: another request created it in between.
    private sealed class RacingUsers(PlatformUser winner) : IPlatformUserRepository
    {
        private int lookups;
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(++lookups == 1 ? null : winner);
        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>([winner]);
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(1);
        public Task AddAsync(PlatformUser user, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RacingUnitOfWork : IUnitOfWork
    {
        public int TryCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) { SaveCalls++; return Task.FromResult(1); }
        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) { TryCalls++; return Task.FromResult(false); }
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
