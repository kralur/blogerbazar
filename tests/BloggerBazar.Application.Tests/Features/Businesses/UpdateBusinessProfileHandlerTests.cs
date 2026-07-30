using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Businesses;
using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Tests.Features.Businesses;

public sealed class UpdateBusinessProfileHandlerTests
{
    [Fact]
    public async Task Updates_the_profile_owned_by_the_telegram_user()
    {
        var business = BusinessProfile.Create(101, "Old name", "Tashkent");
        var unitOfWork = new SpyUnitOfWork();
        var handler = new UpdateBusinessProfileHandler(new InMemoryBusinessRepository(business), unitOfWork);

        var result = await handler.Handle(
            new UpdateBusinessProfileCommand(101, "Lumi Beauty", "@lumi", "Samarkand", "https://cdn.example/logo.png", "https://lumi.uz", "Beauty brand", "+998901234567", "hello@lumi.uz"),
            CancellationToken.None);

        Assert.Equal("Lumi Beauty", result.Name);
        Assert.Equal("@lumi", result.Username);
        Assert.Equal("Samarkand", result.City);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Rejects_update_without_an_existing_profile()
    {
        var handler = new UpdateBusinessProfileHandler(new InMemoryBusinessRepository(null), new SpyUnitOfWork());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new UpdateBusinessProfileCommand(101, "Lumi Beauty", null, null, null, null, null, null, null),
            CancellationToken.None));
    }

    private sealed class InMemoryBusinessRepository(BusinessProfile? business) : IBusinessProfileRepository
    {
        public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(business?.Id == id ? business : null);
        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) =>
            Task.FromResult(business?.TelegramUserId == telegramUserId ? business : null);
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }
    }
}
