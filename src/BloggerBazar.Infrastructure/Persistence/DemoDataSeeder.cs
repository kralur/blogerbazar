using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Infrastructure.Persistence;

// Demo cards for production before the launch (owner 2026-11-10: «до рекламы можно в прод, потом удалим»). Unlike the
// development dataset: invented people and companies only (no real brands), and no contacts at all (no phone,
// @username, email or social link), so nobody can be called or written to through a demo card. Every demo account has
// a Telegram id in a range no real account has; removal deletes exactly that range with everything attached to it.
// Switched by DemoData:Enabled: true adds the cards once, false removes them; absent does nothing.
public static class DemoDataSeeder
{
    public const long TelegramIdStart = 900_000_000_000;
    public const long TelegramIdEnd = 901_000_000_000;
    private const long BloggerIdStart = TelegramIdStart;
    private const long BusinessIdStart = TelegramIdStart + 100_000;
    private const long BrandFaceIdStart = TelegramIdStart + 200_000;

    private static readonly string[] Cities = ["tashkent-city", "samarkand", "bukhara", "fergana", "namangan", "andijan", "navoi", "khorezm"];
    private static readonly string[] Categories = ["lifestyle", "beauty", "fashion", "food", "travel", "technology", "finance", "sport", "gaming", "other:kids", "other:education", "other:auto"];
    private static readonly string[] Platforms = ["Instagram", "Telegram", "TikTok", "YouTube"];

    private static readonly (string Name, string LastName, string Gender)[] People =
    [
        ("Ozoda", "Qodirova", "Female"), ("Sitora", "Nurmatova", "Female"), ("Gulnora", "Toshpulatova", "Female"), ("Munisa", "Xolmatova", "Female"),
        ("Robiya", "Sobirova", "Female"), ("Feruza", "Jo‘rayeva", "Female"), ("Dildora", "Baxromova", "Female"), ("Kamola", "Ergasheva", "Female"),
        ("Charos", "Mirzayeva", "Female"), ("Hilola", "Rustamova", "Female"), ("Zebo", "Hamidova", "Female"), ("Nargiza", "Sultonova", "Female"),
        ("Shahnoza", "Islomova", "Female"), ("Durdona", "Valiyeva", "Female"), ("Maftuna", "Olimova", "Female"), ("Iroda", "Xasanova", "Female"),
        ("Sanjar", "Qosimov", "Male"), ("Otabek", "Norqulov", "Male"), ("Jasur", "Tojiboyev", "Male"), ("Sherzod", "Abdurahmonov", "Male"),
        ("Bobur", "Yo‘ldoshev", "Male"), ("Islom", "Karimberdiyev", "Male"), ("Elyor", "Shodmonov", "Male"), ("Javlon", "Ahrorov", "Male"),
        ("Doniyor", "Mahmudov", "Male"), ("Farrux", "Zokirov", "Male"), ("Ulug‘bek", "Berdiyev", "Male"), ("Shoxrux", "Nematov", "Male"),
        ("Nodirbek", "Sattorov", "Male"), ("Azamat", "Raxmonov", "Male"), ("Alisher", "Usmonov", "Male"), ("Behruz", "Qahhorov", "Male"),
        ("Madina", "Ortiqova", "Female"), ("Lobar", "Ismatova", "Female"), ("Diyora", "Saidova", "Female"), ("Sabina", "Aminova", "Female"),
        ("Akbar", "Fozilov", "Male"), ("Jahongir", "Pardayev", "Male"), ("Mirjalol", "Odilov", "Male"), ("Asilbek", "Turg‘unov", "Male")
    ];

    // Invented names: two ordinary Uzbek words with a kind of business, not any known brand.
    private static readonly string[] Companies =
    [
        "Shirin Non Bakery", "Oydin Studio", "Nurli Tabassum Dental", "Yashil Bozor Market", "Ufq Travel", "Tong Fitness", "Lola Gullari",
        "Ilm Yo‘li Academy", "Sayyor Digital", "Mehr Klinika", "Uyim Home Decor", "Do‘stlik Coffee", "Kamalak Kids", "Ozod Avto Servis", "Saodat Beauty Bar"
    ];

    private static readonly string[] CampaignThemes =
    [
        "осенняя акция", "честный обзор", "новое меню", "открытие филиала", "Reels о продукте", "семейные выходные", "полезный совет", "запуск сервиса"
    ];

    private static readonly string[] ReviewComments =
    [
        "Всё прошло спокойно и в срок, контент понравился аудитории.",
        "Понятная задача и быстрая обратная связь.",
        "Хорошая коммуникация, будем работать снова.",
        "Интеграция получилась живой, отчёт пришёл вовремя."
    ];

    public static async Task ApplyAsync(BloggerBazarDbContext dbContext, bool enabled, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (enabled) await SeedAsync(dbContext, logger, cancellationToken);
        else await RemoveAsync(dbContext, logger, cancellationToken);
    }

    public static async Task SeedAsync(BloggerBazarDbContext dbContext, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (await dbContext.BloggerProfiles.IgnoreQueryFilters().AnyAsync(profile => profile.TelegramUserId >= TelegramIdStart && profile.TelegramUserId < TelegramIdEnd, cancellationToken))
        {
            logger.LogInformation("Demo cards are already there.");
            return;
        }

        var bloggers = People.Select((person, index) => CreateBlogger(person, index)).ToList();
        var businesses = Companies.Select((name, index) => CreateBusiness(name, index)).ToList();
        var brandFaces = Enumerable.Range(0, 10).Select(CreateBrandFace).ToList();
        var platforms = bloggers.SelectMany((blogger, index) => Enumerable.Range(0, 2).Select(offset =>
            SocialPlatform.Create(blogger.Id, Platforms[(index + offset) % Platforms.Length], string.Empty, Math.Max(1_000, blogger.TotalFollowers - offset * 4_000), null))).ToList();
        var campaigns = businesses.SelectMany((business, index) => Enumerable.Range(0, 2).Select(offset => CreateCampaign(business, index * 2 + offset))).ToList();

        // Finished deals with reviews on both sides, so ratings and "deals done" look like a working market.
        var applications = new List<CampaignApplication>();
        var deals = new List<Deal>();
        var reviews = new List<Review>();
        for (var index = 0; index < 24; index++)
        {
            var campaign = campaigns[index % campaigns.Count];
            var blogger = bloggers[(index * 7) % bloggers.Count];
            var business = businesses.Single(candidate => candidate.Id == campaign.BusinessId);
            var application = CampaignApplication.Create(campaign.Id, blogger.Id, "Готов(а) сделать нативную интеграцию и прислать статистику.");
            application.Accept();
            var deal = Deal.Create(application.Id, blogger.Id, business.Id, CampaignTermsSnapshot.FromCampaign(campaign));
            deal.Complete();
            applications.Add(application);
            deals.Add(deal);
            var bloggerReview = Review.ForBlogger(deal.Id, business.TelegramUserId, blogger.Id, index % 6 == 0 ? 4 : 5, ReviewComments[index % ReviewComments.Length]);
            var businessReview = Review.ForBusiness(deal.Id, blogger.TelegramUserId, business.Id, index % 5 == 0 ? 4 : 5, ReviewComments[(index + 1) % ReviewComments.Length]);
            bloggerReview.Publish(bloggerReview.CreatedAtUtc);
            businessReview.Publish(businessReview.CreatedAtUtc);
            reviews.Add(bloggerReview);
            reviews.Add(businessReview);
        }

        await dbContext.BloggerProfiles.AddRangeAsync(bloggers, cancellationToken);
        await dbContext.BusinessProfiles.AddRangeAsync(businesses, cancellationToken);
        await dbContext.BrandFaceProfiles.AddRangeAsync(brandFaces, cancellationToken);
        await dbContext.SocialPlatforms.AddRangeAsync(platforms, cancellationToken);
        await dbContext.Campaigns.AddRangeAsync(campaigns, cancellationToken);
        await dbContext.CampaignApplications.AddRangeAsync(applications, cancellationToken);
        await dbContext.Deals.AddRangeAsync(deals, cancellationToken);
        await dbContext.Reviews.AddRangeAsync(reviews, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Demo cards added: {Bloggers} bloggers, {BrandFaces} brand faces, {Companies} companies, {Campaigns} campaigns.",
            bloggers.Count, brandFaces.Count, businesses.Count, campaigns.Count);
    }

    // Everything of the demo accounts, in the order the foreign keys need; real people's answers to demo campaigns and
    // offers to demo cards go with them.
    public static async Task RemoveAsync(BloggerBazarDbContext dbContext, ILogger logger, CancellationToken cancellationToken = default)
    {
        var bloggerIds = await dbContext.BloggerProfiles.IgnoreQueryFilters().Where(profile => profile.TelegramUserId >= TelegramIdStart && profile.TelegramUserId < TelegramIdEnd).Select(profile => profile.Id).ToListAsync(cancellationToken);
        var businessIds = await dbContext.BusinessProfiles.IgnoreQueryFilters().Where(profile => profile.TelegramUserId >= TelegramIdStart && profile.TelegramUserId < TelegramIdEnd).Select(profile => profile.Id).ToListAsync(cancellationToken);
        var brandFaceIds = await dbContext.BrandFaceProfiles.IgnoreQueryFilters().Where(profile => profile.TelegramUserId >= TelegramIdStart && profile.TelegramUserId < TelegramIdEnd).Select(profile => profile.Id).ToListAsync(cancellationToken);
        if (bloggerIds.Count == 0 && businessIds.Count == 0 && brandFaceIds.Count == 0)
        {
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var dealIds = await dbContext.Deals.IgnoreQueryFilters()
            .Where(deal => businessIds.Contains(deal.BusinessId) || (deal.BloggerId != null && bloggerIds.Contains(deal.BloggerId.Value)) || (deal.BrandFaceId != null && brandFaceIds.Contains(deal.BrandFaceId.Value)))
            .Select(deal => deal.Id).ToListAsync(cancellationToken);
        await dbContext.Reviews.IgnoreQueryFilters()
            .Where(review => dealIds.Contains(review.DealId)
                || (review.BloggerId != null && bloggerIds.Contains(review.BloggerId.Value))
                || (review.BusinessId != null && businessIds.Contains(review.BusinessId.Value))
                || (review.BrandFaceId != null && brandFaceIds.Contains(review.BrandFaceId.Value)))
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.Deals.IgnoreQueryFilters().Where(deal => dealIds.Contains(deal.Id)).ExecuteDeleteAsync(cancellationToken);
        // Campaigns, answers, offers, platforms, favorites and team rows go with their profiles (cascade).
        await dbContext.BloggerProfiles.IgnoreQueryFilters().Where(profile => bloggerIds.Contains(profile.Id)).ExecuteDeleteAsync(cancellationToken);
        await dbContext.BrandFaceProfiles.IgnoreQueryFilters().Where(profile => brandFaceIds.Contains(profile.Id)).ExecuteDeleteAsync(cancellationToken);
        await dbContext.BusinessProfiles.IgnoreQueryFilters().Where(profile => businessIds.Contains(profile.Id)).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Demo cards removed: {Bloggers} bloggers, {BrandFaces} brand faces, {Companies} companies.", bloggerIds.Count, brandFaceIds.Count, businessIds.Count);
    }

    private static BloggerProfile CreateBlogger((string Name, string LastName, string Gender) person, int index)
    {
        var city = Cities[index % Cities.Length];
        var categories = CategoriesFor(index);
        var followers = 8_000 + index * 23_500 % 640_000;
        var reach = Math.Max(1_500, followers * (14 + index % 20) / 100);
        var engagement = decimal.Round(2.8m + index * 37 % 610 / 100m, 2);
        var stories = 150_000 + followers / 10 / 1_000 * 1_000;
        var blogger = BloggerProfile.Create(BloggerIdStart + index, person.Name, city, categories);
        blogger.UpdatePublicProfile(
            person.Name, person.LastName, null, city, categories,
            Bio(person.Name, categories, index),
            $"https://api.dicebear.com/9.x/notionists/svg?seed=demo-blogger-{index}&backgroundColor=c0aede,d1d4f9,ffd5dc,ffdfbf",
            null, null,
            followers, reach, engagement,
            stories, stories + 150_000, stories + 90_000, stories + 320_000,
            index % 6 == 0);
        blogger.UpdateExtendedProfile(null, 19 + index % 16, person.Gender, index % 3 == 0 ? "uz" : index % 3 == 1 ? "ru" : "uz,ru", categories[0], stories, stories + 320_000,
            index % 6 == 0 ? "Открыт(а) к бартеру и долгим проектам." : "Работаю с понятной статистикой после публикации.");
        blogger.Approve();
        blogger.SetPromotion(index % 11 == 0);
        return blogger;
    }

    private static BusinessProfile CreateBusiness(string name, int index)
    {
        var city = Cities[(index * 3) % Cities.Length];
        var business = BusinessProfile.Create(BusinessIdStart + index, name, city);
        business.Update(name, null, city, $"https://api.dicebear.com/9.x/initials/svg?seed={Uri.EscapeDataString(name)}&backgroundColor=bfdbfe,cffafe,fef3c7", null,
            $"{name}: работаем для жителей региона и ищем авторов для честных рекламных интеграций.", null, null);
        business.Approve(verified: false);
        return business;
    }

    private static BrandFaceProfile CreateBrandFace(int index)
    {
        var person = People[(index * 3 + 1) % People.Length];
        var city = Cities[(index + 2) % Cities.Length];
        var categories = CategoriesFor(index + 3);
        var profile = BrandFaceProfile.Create(BrandFaceIdStart + index, person.Name, city, categories);
        profile.Update(person.Name, city, 21 + index % 12, person.Gender, index % 2 == 0 ? ["ru", "uz"] : ["uz"], categories,
            "Съёмки для каталогов, роликов и наружной рекламы.", null, null, null, 400_000 + index * 60_000,
            $"Бренд-фейс из региона. Открыт(а) к коммерческим съёмкам.",
            $"https://api.dicebear.com/9.x/notionists/svg?seed=demo-face-{index}&backgroundColor=b6e3f4,c0aede,d1d4f9");
        profile.SetPromotion(index % 5 == 0);
        return profile;
    }

    private static Campaign CreateCampaign(BusinessProfile business, int index)
    {
        var categories = CategoriesFor(index + 5);
        var campaign = Campaign.Create(
            business.Id,
            $"{business.Name}: {CampaignThemes[index % CampaignThemes.Length]}",
            $"{business.Name} ищет авторов о {categories[0]}. Нужен живой формат: Reels, Stories или короткий обзор с рекламной маркировкой.",
            categories,
            ["Нативная интеграция", "Статистика охвата через 48 часов", "Согласование текста до публикации"],
            700_000 + index * 55_000,
            1_400_000 + index * 70_000,
            Cities[index % Cities.Length],
            DateTime.UtcNow.Date.AddDays(10 + index % 40));
        campaign.Publish();
        campaign.SetPromotion(index % 9 == 0);
        return campaign;
    }

    private static string[] CategoriesFor(int index)
    {
        var first = Categories[index % Categories.Length];
        var second = Categories[(index * 5 + 3) % Categories.Length];
        return first == second ? [first, Categories[(index + 1) % Categories.Length]] : [first, second];
    }

    private static string Bio(string name, IReadOnlyList<string> categories, int index) => (index % 3) switch
    {
        0 => $"{name}: снимаю короткие видео о {categories[0]} и {categories[1]}.",
        1 => $"Блог о {categories[0]} и жизни в городе, делюсь находками каждый день.",
        _ => $"Живая аудитория, честные обзоры о {categories[0]}."
    };
}
