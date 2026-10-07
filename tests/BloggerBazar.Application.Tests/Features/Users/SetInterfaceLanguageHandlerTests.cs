using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Users;
using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Tests.Features.Users;

public sealed class SetInterfaceLanguageHandlerTests
{
    [Fact]
    public async Task Stores_the_language_and_returns_it()
    {
        var user = PlatformUser.Create(42, "Umid", null);
        var unitOfWork = new CountingUnitOfWork();
        var handler = new SetInterfaceLanguageHandler(new InMemoryUsers(user), unitOfWork);

        var result = await handler.Handle(new SetInterfaceLanguageCommand(42, "uz"), CancellationToken.None);

        Assert.Equal("uz", user.PreferredLanguage);
        Assert.Equal("uz", result.PreferredLanguage);
        Assert.Equal(1, unitOfWork.Saves);
    }

    [Fact]
    public async Task Does_not_save_when_the_language_is_unchanged()
    {
        var user = PlatformUser.Create(42, "Umid", null);
        user.SetPreferredLanguage("ru");
        var unitOfWork = new CountingUnitOfWork();
        var handler = new SetInterfaceLanguageHandler(new InMemoryUsers(user), unitOfWork);

        await handler.Handle(new SetInterfaceLanguageCommand(42, "ru"), CancellationToken.None);

        Assert.Equal(0, unitOfWork.Saves);
    }

    [Fact]
    public async Task Unknown_user_is_not_found()
    {
        var handler = new SetInterfaceLanguageHandler(new InMemoryUsers(), new CountingUnitOfWork());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new SetInterfaceLanguageCommand(42, "ru"), CancellationToken.None));

        Assert.Contains("not found", exception.Message);
    }

    [Theory]
    [InlineData("ru", true)]
    [InlineData("uz", true)]
    [InlineData("en", false)]
    [InlineData("RU", false)]
    [InlineData("", false)]
    public void Validator_accepts_only_supported_languages(string language, bool valid) =>
        Assert.Equal(valid, new SetInterfaceLanguageValidator().Validate(new SetInterfaceLanguageCommand(42, language)).IsValid);

    [Fact]
    public void Domain_rejects_unsupported_language() =>
        Assert.Throws<ArgumentException>(() => PlatformUser.Create(42, "Umid", null).SetPreferredLanguage("en"));

    private sealed class InMemoryUsers(params PlatformUser[] users) : IPlatformUserRepository
    {
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(users.SingleOrDefault(user => user.TelegramUserId == telegramUserId));
        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>(users);
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(users.Length);
        public Task AddAsync(PlatformUser user, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CountingUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) { Saves++; return Task.FromResult(1); }
    }
}
