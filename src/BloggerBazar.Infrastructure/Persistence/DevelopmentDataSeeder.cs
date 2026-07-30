using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Infrastructure.Persistence;

public static class DevelopmentDataSeeder
{
    private const int BloggerTarget = 360;
    private const int BusinessTarget = 120;
    private const int CampaignTarget = 360;
    private const int ApplicationTarget = 1_200;
    private const int DealTarget = 500;
    private const long BloggerTelegramIdStart = 50_000_000_000;
    private const long BusinessTelegramIdStart = 60_000_000_000;
    private const long BrandFaceTelegramIdStart = 70_000_000_000;

    private static readonly string[] Cities =
    [
        "Tashkent", "Samarkand", "Bukhara", "Navoi", "Fergana", "Namangan", "Andijan", "Urgench", "Karshi", "Nukus", "Jizzakh"
    ];

    private static readonly string[] Categories =
    [
        "Lifestyle", "Beauty", "Fashion", "Food", "Travel", "Tech", "Auto", "Finance", "Business", "Sport", "Gaming", "Medicine", "Education", "Music", "Comedy", "Kids", "Pets"
    ];

    private static readonly string[] Platforms = ["Instagram", "Telegram", "TikTok", "YouTube"];

    private static readonly (string FirstName, string Gender)[] FirstNames =
    [
        ("Aziza", "Female"), ("Madina", "Female"), ("Dilnoza", "Female"), ("Shahzoda", "Female"),
        ("Malika", "Female"), ("Nodira", "Female"), ("Zarina", "Female"), ("Laylo", "Female"),
        ("Sevara", "Female"), ("Mubina", "Female"), ("Nilufar", "Female"), ("Farangiz", "Female"),
        ("Asadbek", "Male"), ("Javohir", "Male"), ("Bekzod", "Male"), ("Sardor", "Male"),
        ("Miraziz", "Male"), ("Kamron", "Male"), ("Umid", "Male"), ("Shahboz", "Male"),
        ("Akmal", "Male"), ("Diyor", "Male"), ("Temur", "Male"), ("Rustam", "Male")
    ];

    private static readonly string[] LastNames =
    [
        "Karimova", "Rakhimova", "Usmanova", "Yuldasheva", "Abdullaeva", "Mamatkulova", "Tursunova", "Saidova",
        "Nazarov", "Kadirov", "Rasulov", "Akhmedov", "Murodov", "Alimuhamedov", "Ismoilov", "Ergashev"
    ];

    private static readonly string[] BusinessBrands =
    [
        "Uzum", "Payme", "Click", "Beeline Uzbekistan", "Ucell", "Humans", "Artel", "Texnomart", "Korzinka", "EVOS",
        "Bellissimo Pizza", "Oqtepa Lavash", "Pepsi Uzbekistan", "Coca-Cola Uzbekistan", "BYD Uzbekistan", "KIA Uzbekistan", "Haval Uzbekistan", "Asaxiy", "Idea", "Makro",
        "Safia Cafe & Bakery", "Les Ailes", "Feed Up", "Black Star Burger Tashkent", "L'Occitane Uzbekistan", "Mediapark", "ZoodMall", "Yandex Go Uzbekistan", "Express 24", "Didox",
        "TBC Bank Uzbekistan", "Hamkorbank", "Kapitalbank", "Anorbank", "Ipak Yuli Bank", "Uztelecom", "Perfectum", "Rayhon", "MUMUSO Uzbekistan", "Miniso Uzbekistan",
        "Samarqand Silk", "Tashkent City Mall", "Magic City", "Silk Road Samarkand", "Hyatt Regency Tashkent", "Wyndham Tashkent", "International Hotel Tashkent", "Crafers", "KFC Uzbekistan", "Baskin Robbins Uzbekistan",
        "Choyxona 24", "Tashkent Coffee", "Atlas Wear", "Ustoz Academy", "Orzu Travel", "Barkamol Auto", "Ipak Yoli Hotel", "Meros Books", "BeFit Studio", "Ziyo School"
    ];

    private static readonly string[] LocalBusinessTypes =
    [
        "Coffee", "Studio", "Market", "Academy", "Dental", "Flowers", "Fitness", "Bakery", "Digital", "Clinic", "Home", "Travel"
    ];

    private static readonly string[] CampaignThemes =
    [
        "летняя акция", "честный обзор", "новая коллекция", "городской гид", "тест-драйв", "Back to School", "видео-рецепт", "семейный уикенд", "полезный совет", "сезонное меню", "креативный Reels", "запуск сервиса"
    ];

    private static readonly string[] ApplicationMessages =
    [
        "Готов(а) предложить нативный формат и прислать статистику после публикации.",
        "Моя аудитория хорошо совпадает с этой категорией. Могу обсудить сценарий.",
        "Есть опыт интеграций в этой нише и свободное окно в ближайшие две недели.",
        "Предлагаю короткий Reels и серию Stories с понятным призывом к действию.",
        "Готов(а) подготовить искренний обзор и согласовать материалы до выхода.",
        "Могу сделать контент на русском и узбекском языках.",
        "Аудитория активно реагирует на рекомендации в этой категории.",
        "Готов(а) обсудить бартерный или коммерческий формат сотрудничества.",
        "Могу показать продукт в ежедневном контенте без навязчивой рекламы.",
        "Есть релевантные кейсы и понятная статистика прошлых интеграций.",
        "Подготовлю аккуратную интеграцию с маркировкой и отчётом по охвату.",
        "Буду рад(а) обсудить задачу и предложить подходящий формат."
    ];

    private static readonly string[] BloggerReviewComments =
    [
        "Оплата пришла вовремя, а работа была организована очень спокойно.",
        "Высокий ER и внимательное отношение к деталям кампании.",
        "Контент получился живым, аудитория хорошо отреагировала.",
        "Быстро согласовали формат и получили понятный отчёт.",
        "Отличная коммуникация и аккуратное соблюдение сроков.",
        "Интеграция выглядела нативно и принесла качественные отклики.",
        "Хотелось бы чуть быстрее получить финальную статистику, но результат отличный.",
        "Будем сотрудничать снова: всё прошло профессионально.",
        "Автор хорошо понял задачу и предложил сильную креативную идею.",
        "Честный обзор, хорошая вовлечённость и прозрачная коммуникация."
    ];

    private static readonly string[] BusinessReviewComments =
    [
        "Чёткое техническое задание и быстрая обратная связь.",
        "Команда заранее согласовала все детали и соблюдала договорённости.",
        "Удобный процесс, понятные сроки и уважительное общение.",
        "Бренд подготовил хорошие материалы и не задерживал согласование.",
        "Приятное сотрудничество, будем рады новым совместным кампаниям.",
        "Задача была сформулирована ясно, коммуникация прошла без лишних правок.",
        "Хотелось бы больше времени на подготовку, но поддержка команды была отличной.",
        "Оплата и организационные вопросы были закрыты вовремя.",
        "Бизнес внимательно отнёсся к аудитории и сохранил нативность формата.",
        "Хорошо выстроенный процесс от брифа до финального отчёта."
    ];

    public static async Task SeedAsync(BloggerBazarDbContext dbContext, ILogger logger, CancellationToken cancellationToken = default)
    {
        await SeedOwnerAsync(dbContext, cancellationToken);
        await SeedBrandFacesAsync(dbContext, cancellationToken);
        if (await dbContext.BloggerProfiles.AnyAsync(profile => profile.TelegramUserId >= BloggerTelegramIdStart, cancellationToken))
        {
            logger.LogInformation("BloggerBazar v1.0 development dataset already exists; seeding is skipped.");
            return;
        }

        var bloggers = CreateBloggers();
        var businesses = CreateBusinesses();
        var campaigns = CreateCampaigns(businesses);
        var platforms = CreatePlatforms(bloggers);
        var portfolioItems = CreatePortfolioItems(bloggers);
        var applications = CreateApplications(campaigns, bloggers);
        var deals = CreateCompletedDeals(applications, campaigns);
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
            "Seeded BloggerBazar v1.0 development dataset: {BloggerCount} bloggers, {BusinessCount} businesses, {CampaignCount} campaigns, {ApplicationCount} applications, {DealCount} completed deals, {ReviewCount} reviews.",
            bloggers.Count,
            businesses.Count,
            campaigns.Count,
            applications.Count,
            deals.Count,
            reviews.Count);
    }

    private static async Task SeedBrandFacesAsync(BloggerBazarDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.BrandFaceProfiles.AnyAsync(profile => profile.TelegramUserId >= BrandFaceTelegramIdStart, cancellationToken))
        {
            return;
        }

        var profiles = Enumerable.Range(0, 24).Select(index =>
        {
            var (name, gender) = FirstNames[index % FirstNames.Length];
            var city = Cities[(index * 3) % Cities.Length];
            var categories = GetCategories(index + 2);
            var username = $"{name.ToLowerInvariant()}_face_{index + 1}";
            var profile = BrandFaceProfile.Create(BrandFaceTelegramIdStart + index, name, city, categories);
            profile.Update(
                name,
                city,
                20 + index % 14,
                gender,
                index % 2 == 0 ? ["ru", "uz"] : ["uz"],
                categories,
                $"{name} представляет бренды в категориях {categories[0]} и {categories[1]}. Есть опыт съёмок, презентаций и рекламных интеграций.",
                $"@{username}_ig",
                $"@{username}",
                $"https://example.com/portfolio/{username}",
                350_000 + index * 45_000,
                $"Бренд-фейс из {city}. Открыт(а) к коммерческим кампаниям и долгосрочному сотрудничеству.",
                $"https://api.dicebear.com/9.x/avataaars/svg?seed=brand-face-{username}&backgroundColor=b6e3f4,c0aede,d1d4f9");
            profile.SetPromotion(index % 8 == 0);
            return profile;
        }).ToArray();

        await dbContext.BrandFaceProfiles.AddRangeAsync(profiles, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedOwnerAsync(BloggerBazarDbContext dbContext, CancellationToken cancellationToken)
    {
        const long ownerTelegramUserId = 5044343262;
        var owner = await dbContext.PlatformUsers.SingleOrDefaultAsync(user => user.TelegramUserId == ownerTelegramUserId, cancellationToken);
        if (owner is null)
        {
            owner = PlatformUser.Create(ownerTelegramUserId, "Умиджон Баходирович", "umidkb");
            owner.SetRole(PlatformRole.Owner);
            await dbContext.PlatformUsers.AddAsync(owner, cancellationToken);
        }
        else if (owner.Role != PlatformRole.Owner)
        {
            owner.SetRole(PlatformRole.Owner);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static List<BloggerProfile> CreateBloggers()
    {
        var bloggers = new List<BloggerProfile>(BloggerTarget);
        for (var index = 0; index < BloggerTarget; index++)
        {
            var (firstName, gender) = FirstNames[index % FirstNames.Length];
            var lastName = LastNames[index / FirstNames.Length];
            var username = $"{firstName.ToLowerInvariant()}_{lastName.ToLowerInvariant()}{index + 101}";
            var city = Cities[index % Cities.Length];
            var categories = GetCategories(index);
            var followers = 12_000 + (index * 19_700 % 1_250_000);
            var engagementRate = decimal.Round(2.6m + (index * 37 % 720) / 100m, 2);
            var averageReach = Math.Max(2_000, followers * (12 + index % 24) / 100);
            var storiesPrice = 160_000 + followers / 8;
            var reelsPrice = storiesPrice + 170_000 + index % 7 * 20_000;
            var postPrice = storiesPrice + 95_000;
            var integrationPrice = reelsPrice + 145_000;
            var blogger = BloggerProfile.Create(BloggerTelegramIdStart + index, firstName, city, categories);

            blogger.UpdatePublicProfile(
                firstName,
                lastName,
                username,
                city,
                categories,
                BuildBloggerBio(firstName, city, categories, index),
                $"https://api.dicebear.com/9.x/avataaars/svg?seed={username}&backgroundColor=b6e3f4,c0aede,d1d4f9",
                $"+99890{1_000_000 + index:D7}",
                $"{username}@demo.bloggerbazar.uz",
                followers,
                averageReach,
                engagementRate,
                storiesPrice,
                reelsPrice,
                postPrice,
                integrationPrice,
                index % 9 == 0);
            blogger.UpdateExtendedProfile(
                $"https://api.dicebear.com/9.x/shapes/svg?seed=cover-{username}",
                20 + index % 15,
                gender,
                index % 3 == 0 ? "uz" : index % 3 == 1 ? "ru" : "uz,ru",
                categories[0],
                storiesPrice,
                integrationPrice,
                index % 9 == 0 ? "Открыт(а) к бартерным и долгосрочным интеграциям." : "Работаю по коммерческим интеграциям с прозрачной статистикой.");
            blogger.Approve();
            blogger.SetPromotion(index % 17 == 0);
            bloggers.Add(blogger);
        }

        return bloggers;
    }

    private static List<BusinessProfile> CreateBusinesses()
    {
        var businesses = new List<BusinessProfile>(BusinessTarget);
        for (var index = 0; index < BusinessTarget; index++)
        {
            var name = GetBusinessName(index);
            var city = Cities[index % Cities.Length];
            var username = ToSlug(name);
            var business = BusinessProfile.Create(BusinessTelegramIdStart + index, name, city);
            business.Update(
                name,
                username,
                city,
                $"https://api.dicebear.com/9.x/initials/svg?seed={Uri.EscapeDataString(name)}&backgroundColor=bfdbfe,cffafe,fef3c7",
                $"https://{username}.uz",
                BuildBusinessDescription(name, city, index),
                $"+99871{2_000_000 + index:D7}",
                $"hello@{username}.uz");
            business.Approve();
            businesses.Add(business);
        }

        return businesses;
    }

    private static List<Campaign> CreateCampaigns(IReadOnlyList<BusinessProfile> businesses)
    {
        var campaigns = new List<Campaign>(CampaignTarget);
        for (var index = 0; index < CampaignTarget; index++)
        {
            var business = businesses[index % businesses.Count];
            var categories = GetCategories(index + 4);
            var title = $"{business.Name}: {CampaignThemes[index % CampaignThemes.Length]}";
            var campaign = Campaign.Create(
                business.Id,
                title,
                BuildCampaignDescription(business.Name, categories, index),
                categories,
                BuildRequirements(categories, index),
                600_000 + index * 42_000,
                1_200_000 + index * 58_000,
                Cities[(index * 3) % Cities.Length],
                DateTime.UtcNow.Date.AddDays(7 + index % 45));

            if (index < 320)
            {
                campaign.Publish();
                campaign.SetPromotion(index % 13 == 0);
            }
            else if (index < 342)
            {
                campaign.Publish();
                campaign.Archive();
            }

            campaigns.Add(campaign);
        }

        return campaigns;
    }

    private static List<SocialPlatform> CreatePlatforms(IReadOnlyList<BloggerProfile> bloggers)
    {
        return bloggers.SelectMany((blogger, bloggerIndex) => Enumerable.Range(0, 2).Select(platformIndex =>
        {
            var platform = Platforms[(bloggerIndex + platformIndex) % Platforms.Length];
            return SocialPlatform.Create(
                blogger.Id,
                platform,
                $"https://{platform.ToLowerInvariant()}.com/{blogger.Username}",
                Math.Max(1_000, blogger.TotalFollowers - platformIndex * (1_800 + bloggerIndex % 2_500)),
                null);
        })).ToList();
    }

    private static List<PortfolioItem> CreatePortfolioItems(IReadOnlyList<BloggerProfile> bloggers)
    {
        return bloggers.SelectMany((blogger, bloggerIndex) => Enumerable.Range(0, 2).Select(itemIndex =>
            PortfolioItem.Create(
                blogger.Id,
                $"{blogger.Categories.ElementAt(itemIndex)}: рекламная интеграция",
                (bloggerIndex + itemIndex) % 3 == 0 ? PortfolioItemType.Video : PortfolioItemType.Image,
                $"https://api.dicebear.com/9.x/shapes/svg?seed=portfolio-{blogger.Username}-{itemIndex}"))).ToList();
    }

    private static List<CampaignApplication> CreateApplications(IReadOnlyList<Campaign> campaigns, IReadOnlyList<BloggerProfile> bloggers)
    {
        var applications = new List<CampaignApplication>(ApplicationTarget);
        for (var index = 0; index < ApplicationTarget; index++)
        {
            var campaignIndex = index % campaigns.Count;
            var bloggerIndex = (campaignIndex * 37 + index / campaigns.Count) % bloggers.Count;
            var application = CampaignApplication.Create(campaigns[campaignIndex].Id, bloggers[bloggerIndex].Id, ApplicationMessages[index % ApplicationMessages.Length]);
            if (index < DealTarget)
            {
                application.Accept();
            }
            else if (index < 820)
            {
                application.Reject();
            }
            else if (index < 1_060)
            {
                application.MarkViewed();
            }

            applications.Add(application);
        }

        return applications;
    }

    private static List<Deal> CreateCompletedDeals(IReadOnlyList<CampaignApplication> applications, IReadOnlyList<Campaign> campaigns)
    {
        var deals = new List<Deal>(DealTarget);
        for (var index = 0; index < DealTarget; index++)
        {
            var campaign = campaigns[index % campaigns.Count];
            var deal = Deal.Create(applications[index].Id, applications[index].BloggerId, campaign.BusinessId);
            deal.Complete();
            deals.Add(deal);
        }

        return deals;
    }

    private static List<Review> CreateReviews(IReadOnlyList<Deal> deals, IReadOnlyList<BloggerProfile> bloggers, IReadOnlyList<BusinessProfile> businesses)
    {
        var bloggerById = bloggers.ToDictionary(blogger => blogger.Id);
        var businessById = businesses.ToDictionary(business => business.Id);
        var reviews = new List<Review>(DealTarget * 2);
        for (var index = 0; index < deals.Count; index++)
        {
            var deal = deals[index];
            var blogger = bloggerById[deal.BloggerId];
            var business = businessById[deal.BusinessId];
            var bloggerRating = index % 19 == 0 ? 3 : index % 7 == 0 ? 4 : 5;
            var businessRating = index % 23 == 0 ? 3 : index % 6 == 0 ? 4 : 5;
            reviews.Add(Review.ForBlogger(deal.Id, business.TelegramUserId, blogger.Id, bloggerRating, BloggerReviewComments[index % BloggerReviewComments.Length]));
            reviews.Add(Review.ForBusiness(deal.Id, blogger.TelegramUserId, business.Id, businessRating, BusinessReviewComments[index % BusinessReviewComments.Length]));
        }

        return reviews;
    }

    private static string[] GetCategories(int index)
    {
        var primary = Categories[index % Categories.Length];
        var secondary = Categories[(index * 7 + 5) % Categories.Length];
        return primary == secondary ? [primary, Categories[(index + 1) % Categories.Length]] : [primary, secondary];
    }

    private static string GetBusinessName(int index) => index < BusinessBrands.Length
        ? BusinessBrands[index]
        : $"{Cities[index % Cities.Length]} {LocalBusinessTypes[(index * 5) % LocalBusinessTypes.Length]}";

    private static string BuildBloggerBio(string firstName, string city, IReadOnlyList<string> categories, int index) => (index % 5) switch
    {
        0 => $"{firstName} из {city}: создаю искренний контент о {categories[0]} и {categories[1]}.",
        1 => $"Автор из {city}. Показываю {categories[0]} в повседневной жизни и делюсь полезными находками.",
        2 => $"{categories[0]}-контент с живой аудиторией из {city}; открыта к понятным и честным интеграциям.",
        3 => $"Снимаю короткие видео о {categories[0]} и {categories[1]}, работаю с брендами из Узбекистана.",
        _ => $"Блог о {categories[0]}, городской жизни и новых местах в {city}."
    };

    private static string BuildBusinessDescription(string name, string city, int index) => (index % 4) switch
    {
        0 => $"{name} развивает продукты и сервисы для клиентов в {city} и по всему Узбекистану.",
        1 => $"Команда {name} ищет авторов для долгосрочных, прозрачных рекламных интеграций.",
        2 => $"{name} — локальный бренд с вниманием к качеству, сервису и реальному мнению аудитории.",
        _ => $"Компания {name} запускает маркетинговые кампании и сотрудничает с авторами из {city}."
    };

    private static string BuildCampaignDescription(string businessName, IReadOnlyList<string> categories, int index) => (index % 4) switch
    {
        0 => $"{businessName} ищет авторов в категории {categories[0]}. Нужен нативный контент с честным опытом использования продукта.",
        1 => $"Приглашаем блогеров о {categories[0]} и {categories[1]} к кампании {businessName}. Важны качество аудитории и аккуратная подача.",
        2 => $"Запускаем сезонную кампанию {businessName}. Подойдёт формат Reels, Stories или короткого обзора с прозрачной рекламной маркировкой.",
        _ => $"Нужны креативные авторы для {businessName}: покажите продукт в своём естественном контенте и поделитесь итоговой статистикой."
    };

    private static string[] BuildRequirements(IReadOnlyList<string> categories, int index) => (index % 4) switch
    {
        0 => ["Нативная интеграция", "Статистика охвата через 48 часов", "Согласование ключевых фактов до публикации"],
        1 => ["Минимум один Reels или короткое видео", "Аудитория из Узбекистана", $"Опыт в категории {categories[0]}"],
        2 => ["Соблюдение бренд-гайда", "Отметка официального аккаунта", "Отчёт по охвату и переходам"],
        _ => ["Живой авторский формат", "Публикация в согласованный срок", "Готовность ответить на вопросы аудитории"]
    };

    private static string ToSlug(string value)
    {
        var characters = value.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray();
        return string.Join(string.Empty, characters).Trim('-').Replace("--", "-");
    }
}
