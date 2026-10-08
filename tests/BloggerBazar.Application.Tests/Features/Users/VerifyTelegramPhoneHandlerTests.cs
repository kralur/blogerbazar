using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Users;
using BloggerBazar.Application.Validation;
using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Tests.Features.Users;

public sealed class VerifyTelegramPhoneHandlerTests
{
    [Theory]
    [InlineData("998881972929", "+998 88 197 29 29")]
    [InlineData("+998881972929", "+998 88 197 29 29")]
    [InlineData("79161234567", "+79161234567")]
    [InlineData("12345", null)]
    [InlineData("", null)]
    [InlineData("1234567890123456", null)]
    public void Telegram_number_is_normalized_and_foreign_numbers_are_accepted(string raw, string? expected) =>
        Assert.Equal(expected, ContactValidation.NormalizeTelegramPhone(raw));

    [Theory]
    [InlineData("+998 88 197 29 29", true)]
    [InlineData("+79161234567", true)]
    [InlineData("+998881972929", false)]
    [InlineData("88 197 29 29", false)]
    public void Profiles_accept_only_a_number_in_the_verified_format(string phone, bool valid) =>
        Assert.Equal(valid, ContactValidation.IsVerifiedPhone(phone));

    [Fact]
    public async Task Shared_number_is_stored_on_the_user_and_on_existing_profiles()
    {
        var user = PlatformUser.Create(42, "Umid", "umidkb");
        var blogger = BloggerProfile.Create(42, "Umid", "Tashkent", ["Lifestyle"]);
        var business = BusinessProfile.Create(42, "Lumi", "tashkent");
        var unitOfWork = new CountingUnitOfWork();
        var handler = new VerifyTelegramPhoneHandler(new Users(user), new Bloggers(blogger), new Businesses(business), unitOfWork);

        var phone = await handler.Handle(new VerifyTelegramPhoneCommand(42, "Umid", "umidkb", "998881972929"), CancellationToken.None);

        Assert.Equal("+998 88 197 29 29", phone);
        Assert.Equal(phone, user.VerifiedPhone);
        Assert.NotNull(user.PhoneVerifiedAtUtc);
        Assert.Equal(phone, blogger.Phone);
        Assert.Equal(phone, business.Phone);
        Assert.Equal(1, unitOfWork.Saves);
    }

    [Fact]
    public async Task A_number_shared_before_opening_the_app_creates_the_user()
    {
        var users = new Users();
        var handler = new VerifyTelegramPhoneHandler(users, new Bloggers(), new Businesses(), new CountingUnitOfWork());

        await handler.Handle(new VerifyTelegramPhoneCommand(77, "Samir", null, "998901234567"), CancellationToken.None);

        var created = Assert.Single(users.Added);
        Assert.Equal(77, created.TelegramUserId);
        Assert.Equal("+998 90 123 45 67", created.VerifiedPhone);
    }

    [Fact]
    public async Task Deleted_or_blocked_users_and_bad_numbers_change_nothing()
    {
        var deleted = PlatformUser.Create(1, "A", null);
        deleted.SoftDelete(1);
        var blocked = PlatformUser.Create(2, "B", null);
        blocked.SetBlocked(true);
        var unitOfWork = new CountingUnitOfWork();
        var handler = new VerifyTelegramPhoneHandler(new Users(deleted, blocked), new Bloggers(), new Businesses(), unitOfWork);

        Assert.Null(await handler.Handle(new VerifyTelegramPhoneCommand(1, "A", null, "998901234567"), CancellationToken.None));
        Assert.Null(await handler.Handle(new VerifyTelegramPhoneCommand(2, "B", null, "998901234567"), CancellationToken.None));
        Assert.Null(await handler.Handle(new VerifyTelegramPhoneCommand(2, "B", null, "123"), CancellationToken.None));
        Assert.Null(deleted.VerifiedPhone);
        Assert.Null(blocked.VerifiedPhone);
        Assert.Equal(0, unitOfWork.Saves);
    }

    private sealed class Users(params PlatformUser[] initial) : IPlatformUserRepository
    {
        public List<PlatformUser> Added { get; } = [];
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(initial.Concat(Added).SingleOrDefault(user => user.TelegramUserId == telegramUserId));
        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>(initial);
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(initial.Length);
        public Task AddAsync(PlatformUser user, CancellationToken cancellationToken) { Added.Add(user); return Task.CompletedTask; }
    }

    private sealed class Bloggers(params BloggerProfile[] profiles) : IBloggerProfileRepository
    {
        public Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(profiles.SingleOrDefault(profile => profile.Id == id));
        public Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(profiles.SingleOrDefault(profile => profile.TelegramUserId == telegramUserId));
        public Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
        public Task AddAsync(BloggerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class Businesses(params BusinessProfile[] profiles) : IBusinessProfileRepository
    {
        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(profiles.SingleOrDefault(profile => profile.Id == id));
        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(profiles.SingleOrDefault(profile => profile.TelegramUserId == telegramUserId));
        public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CountingUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) { Saves++; return Task.FromResult(1); }
    }
}
