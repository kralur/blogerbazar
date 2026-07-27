using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Infrastructure.Persistence;

public static class DevelopmentDataSeeder
{
    private static readonly string[] Cities =
    [
        "Tashkent", "Samarkand", "Bukhara", "Andijan", "Namangan",
        "Fergana", "Nukus", "Qarshi", "Jizzakh", "Urgench"
    ];

    private static readonly string[] Categories =
    [
        "Food", "Tech", "Beauty", "Fashion", "Travel",
        "Sport", "Gaming", "Finance", "Education", "Lifestyle"
    ];

    private static readonly string[] Platforms = ["Instagram", "Telegram", "TikTok", "YouTube"];

    private static readonly (string FirstName, string LastName)[] BloggerNames =
    [
        ("Aziza", "Karimova"), ("Madina", "Rakhimova"), ("Dilnoza", "Usmanova"), ("Shahzoda", "Yuldasheva"),
        ("Malika", "Abdullaeva"), ("Nodira", "Mamatkulova"), ("Zarina", "Tursunova"), ("Laylo", "Saidova"),
        ("Asadbek", "Nazarov"), ("Javohir", "Kadirov"), ("Bekzod", "Rasulov"), ("Sardor", "Akhmedov"),
        ("Miraziz", "Murodov"), ("Kamron", "Alimuhamedov"), ("Umid", "Ismoilov"), ("Shahboz", "Ergashev"),
        ("Sevara", "Khamidova"), ("Mubina", "Ruzieva"), ("Nilufar", "Amonova"), ("Farangiz", "Tuychieva")
    ];

    private static readonly string[] BusinessNames =
    [
        "Choyxona 24", "Zamin Tech", "Samarqand Silk", "Navruz Beauty", "Tashkent Coffee",
        "Atlas Wear", "Ustoz Academy", "Fergana Fresh", "Orzu Travel", "Paydo Finance"
    ];

    private static readonly string[] CampaignTitles =
    [
        "Весеннее меню Choyxona 24", "Обзор новых гаджетов Zamin Tech", "Шёлк Самарканда", "Летняя коллекция Atlas Wear",
        "Запуск Navruz Beauty", "Маршруты Orzu Travel", "Кофейная неделя Tashkent Coffee", "Курс Ustoz Academy",
        "Ферганские продукты", "Финансовая грамотность Paydo", "Спортивный челлендж", "Гид по Бухаре",
        "Игровой стрим", "Осенний beauty box", "Lifestyle коллаборация"
    ];

    public static async Task SeedAsync(BloggerBazarDbContext dbContext, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (await dbContext.BloggerProfiles.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Development data already exists; seeding is skipped.");
            return;
        }

        var bloggers = CreateBloggers();
        var businesses = CreateBusinesses();
        var campaigns = CreateCampaigns(businesses);
        var platforms = CreatePlatforms(bloggers);
        var portfolioItems = CreatePortfolioItems(bloggers);
        var applications = CreateApplications(campaigns, bloggers);
        var deals = CreateCompletedDeals(applications, bloggers, businesses);
        var reviews = CreateReviews(deals, bloggers, businesses);

        await dbContext.BloggerProfiles.AddRangeAsync(bloggers, cancellationToken);
        await dbContext.BusinessProfiles.AddRangeAsync(businesses, cancellationToken);
        await dbContext.Campaigns.AddRangeAsync(campaigns, cancellationToken);
        await dbContext.SocialPlatforms.AddRangeAsync(platforms, cancellationToken);
        await dbContext.PortfolioItems.AddRangeAsync(portfolioItems, cancellationToken);
        await dbContext.CampaignApplications.AddRangeAsync(applications, cancellationToken);
        await dbContext.Deals.AddRangeAsync(deals, cancellationToken);
        await dbContext.Reviews.AddRangeAsync(reviews, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded development data: {BloggerCount} bloggers, {BusinessCount} businesses, {CampaignCount} campaigns, {DealCount} completed deals.",
            bloggers.Count,
            businesses.Count,
            campaigns.Count,
            deals.Count);
    }

    private static List<BloggerProfile> CreateBloggers()
    {
        var bloggers = new List<BloggerProfile>(BloggerNames.Length);
        for (var index = 0; index < BloggerNames.Length; index++)
        {
            var (firstName, lastName) = BloggerNames[index];
            var username = $"{firstName.ToLowerInvariant()}{lastName.ToLowerInvariant()}";
            var followers = 8_000 + index * 3_250;
            var blogger = BloggerProfile.Create(10_000_000_000 + index, firstName, Cities[index % Cities.Length], [Categories[index % Categories.Length]]);
            blogger.UpdatePublicProfile(
                firstName,
                lastName,
                username,
                Cities[index % Cities.Length],
                [Categories[index % Categories.Length]],
                $"{Categories[index % Categories.Length]} creator from {Cities[index % Cities.Length]}.",
                $"https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=600&q={70 + index % 20}",
                $"+99890{1000000 + index:D7}",
                $"{username}@example.com",
                followers,
                followers / 3,
                4.2m + index * 0.12m,
                180_000 + index * 12_500,
                260_000 + index * 15_000,
                220_000 + index * 14_000,
                320_000 + index * 18_000,
                index % 3 == 0);
            blogger.UpdateExtendedProfile(
                null,
                20 + index % 12,
                index < 8 || index > 15 ? "Female" : "Male",
                index % 2 == 0 ? "ru" : "uz",
                Categories[index % Categories.Length],
                180_000 + index * 12_500,
                320_000 + index * 18_000,
                "Открыт(а) к долгосрочным рекламным интеграциям.");
            blogger.Approve();
            bloggers.Add(blogger);
        }

        return bloggers;
    }

    private static List<BusinessProfile> CreateBusinesses()
    {
        var businesses = new List<BusinessProfile>(BusinessNames.Length);
        for (var index = 0; index < BusinessNames.Length; index++)
        {
            var business = BusinessProfile.Create(20_000_000_000 + index, BusinessNames[index], Cities[index]);
            business.Update(
                BusinessNames[index],
                BusinessNames[index].Replace(" ", string.Empty).ToLowerInvariant(),
                Cities[index],
                $"https://images.unsplash.com/photo-1497366754035-f200968a6e72?auto=format&fit=crop&w=300&q={70 + index % 20}",
                $"Local Uzbek business focused on {Categories[index]}.",
                $"+99871{2000000 + index:D7}",
                $"hello{index + 1}@example.com");
            businesses.Add(business);
        }

        return businesses;
    }

    private static List<Campaign> CreateCampaigns(IReadOnlyList<BusinessProfile> businesses)
    {
        var campaigns = new List<Campaign>(CampaignTitles.Length);
        for (var index = 0; index < CampaignTitles.Length; index++)
        {
            var campaign = Campaign.Create(
                businesses[index % businesses.Count].Id,
                CampaignTitles[index],
                $"Ищем блогеров для кампании «{CampaignTitles[index]}». Нужны нативные форматы, честный контент и вовлечённая аудитория.",
                [Categories[index % Categories.Length]],
                500_000 + index * 75_000,
                900_000 + index * 95_000,
                Cities[index % Cities.Length]);
            campaign.Publish();
            campaign.SetPromotion(index % 4 == 0);
            campaigns.Add(campaign);
        }

        return campaigns;
    }

    private static List<SocialPlatform> CreatePlatforms(IReadOnlyList<BloggerProfile> bloggers)
    {
        return bloggers.SelectMany((blogger, bloggerIndex) => Platforms.Select((platform, platformIndex) =>
            SocialPlatform.Create(
                blogger.Id,
                platform,
                $"https://{platform.ToLowerInvariant()}.com/{blogger.Username}",
                blogger.TotalFollowers - platformIndex * 1_500,
                null))).ToList();
    }

    private static List<PortfolioItem> CreatePortfolioItems(IReadOnlyList<BloggerProfile> bloggers)
    {
        return bloggers.Select((blogger, index) => PortfolioItem.Create(
            blogger.Id,
            $"Рекламная интеграция #{index + 1}",
            index % 4 == 0 ? PortfolioItemType.Video : PortfolioItemType.Image,
            $"https://images.unsplash.com/photo-1487412720507-e7ab37603c6f?auto=format&fit=crop&w=600&q={70 + index % 20}"))
            .ToList();
    }

    private static List<CampaignApplication> CreateApplications(IReadOnlyList<Campaign> campaigns, IReadOnlyList<BloggerProfile> bloggers)
    {
        var applications = new List<CampaignApplication>(10);
        for (var index = 0; index < 10; index++)
        {
            var application = CampaignApplication.Create(campaigns[index].Id, bloggers[index].Id, "Готов(а) обсудить формат и сроки публикации.");
            application.Accept();
            applications.Add(application);
        }

        return applications;
    }

    private static List<Deal> CreateCompletedDeals(IReadOnlyList<CampaignApplication> applications, IReadOnlyList<BloggerProfile> bloggers, IReadOnlyList<BusinessProfile> businesses)
    {
        var deals = new List<Deal>(applications.Count);
        for (var index = 0; index < applications.Count; index++)
        {
            var deal = Deal.Create(applications[index].Id, bloggers[index].Id, businesses[index % businesses.Count].Id);
            deal.Complete();
            deals.Add(deal);
        }

        return deals;
    }

    private static List<Review> CreateReviews(IReadOnlyList<Deal> deals, IReadOnlyList<BloggerProfile> bloggers, IReadOnlyList<BusinessProfile> businesses)
    {
        var reviews = new List<Review>(deals.Count * 2);
        for (var index = 0; index < deals.Count; index++)
        {
            reviews.Add(Review.ForBlogger(deals[index].Id, businesses[index % businesses.Count].TelegramUserId, bloggers[index].Id, 5, "Профессиональный подход и отличный результат."));
            reviews.Add(Review.ForBusiness(deals[index].Id, bloggers[index].TelegramUserId, businesses[index % businesses.Count].Id, 5, "Чёткое ТЗ и быстрая коммуникация."));
        }

        return reviews;
    }
}
