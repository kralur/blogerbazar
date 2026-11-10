using BloggerBazar.Domain.Entities;
using BloggerBazar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BloggerBazar.Application.Tests.Integration;

// Demo cards before the launch: they come without any contact and go away with everything attached to them, while
// real accounts stay.
[Collection(ApiHostCollection.Name)]
public sealed class DemoDataIntegrationTests(BloggerBazarApiFactory factory) : IClassFixture<BloggerBazarApiFactory>
{
    [IntegrationFact]
    public async Task Demo_cards_come_without_contacts_and_leave_without_touching_real_accounts()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        var real = BusinessProfile.Create(1_600_001, "Real Company", "tashkent-city");
        real.Approve(verified: false);
        dbContext.Add(real);
        await dbContext.SaveChangesAsync();

        await DemoDataSeeder.ApplyAsync(dbContext, enabled: true, NullLogger.Instance);
        await DemoDataSeeder.ApplyAsync(dbContext, enabled: true, NullLogger.Instance);

        var demoBloggers = await dbContext.BloggerProfiles.AsNoTracking().Where(InRange<BloggerProfile>()).ToListAsync();
        Assert.Equal(40, demoBloggers.Count);
        Assert.All(demoBloggers, blogger => Assert.True(blogger.Username is null && blogger.Phone is null && blogger.Email is null));
        Assert.All(await dbContext.BusinessProfiles.AsNoTracking().Where(profile => profile.TelegramUserId >= DemoDataSeeder.TelegramIdStart).ToListAsync(),
            company => Assert.True(company.Phone is null && company.Username is null && company.WebsiteUrl is null && company.Email is null));

        dbContext.ChangeTracker.Clear();
        await DemoDataSeeder.ApplyAsync(dbContext, enabled: false, NullLogger.Instance);

        Assert.False(await dbContext.BloggerProfiles.IgnoreQueryFilters().AnyAsync(InRange<BloggerProfile>()));
        Assert.False(await dbContext.BusinessProfiles.IgnoreQueryFilters().AnyAsync(profile => profile.TelegramUserId >= DemoDataSeeder.TelegramIdStart && profile.TelegramUserId < DemoDataSeeder.TelegramIdEnd));
        Assert.False(await dbContext.BrandFaceProfiles.IgnoreQueryFilters().AnyAsync(profile => profile.TelegramUserId >= DemoDataSeeder.TelegramIdStart && profile.TelegramUserId < DemoDataSeeder.TelegramIdEnd));
        Assert.True(await dbContext.BusinessProfiles.AnyAsync(profile => profile.Id == real.Id));
    }

    private static System.Linq.Expressions.Expression<Func<BloggerProfile, bool>> InRange<T>() =>
        profile => profile.TelegramUserId >= DemoDataSeeder.TelegramIdStart && profile.TelegramUserId < DemoDataSeeder.TelegramIdEnd;
}
