using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Infrastructure.Persistence;
using BloggerBazar.Infrastructure.Caching;
using BloggerBazar.Infrastructure.Security;
using BloggerBazar.Infrastructure.Telegram;
using BloggerBazar.Infrastructure.Media;
using BloggerBazar.Infrastructure.Configuration;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Abstractions.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BloggerBazar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres must be configured.");
        services.AddDbContext<BloggerBazarDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IBloggerProfileRepository, BloggerProfileRepository>();
        services.AddScoped<IBusinessProfileRepository, BusinessProfileRepository>();
        services.AddScoped<ICampaignRepository, CampaignRepository>();
        services.AddScoped<ICampaignApplicationRepository, CampaignApplicationRepository>();
        services.AddScoped<IDealRepository, DealRepository>();
        services.AddScoped<IDealPublicationRepository, DealPublicationRepository>();
        services.AddScoped<IComplaintRepository, ComplaintRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IDealReminderRepository, DealReminderRepository>();
        services.AddScoped<IBotTipRepository, BotTipRepository>();
        services.AddScoped<IContactUnlockRepository, ContactUnlockRepository>();
        services.AddScoped<IFavoriteRepository, FavoriteRepository>();
        services.AddScoped<IBrandFaceFavoriteRepository, BrandFaceFavoriteRepository>();
        services.AddScoped<IPortfolioItemRepository, PortfolioItemRepository>();
        services.AddScoped<ICollaborationRequestRepository, CollaborationRequestRepository>();
        services.AddScoped<ISocialPlatformRepository, SocialPlatformRepository>();
        services.AddScoped<IPlatformUserRepository, PlatformUserRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IBrandFaceProfileRepository, BrandFaceProfileRepository>();
        services.AddScoped<IMarketplaceHomeReadModel, MarketplaceHomeReadModel>();
        services.AddScoped<IMarketplaceRecommendationReadModel, MarketplaceRecommendationReadModel>();
        services.AddScoped<IMarketplaceCatalogReadModel, MarketplaceCatalogReadModel>();
        services.AddScoped<ICampaignCatalogReadModel, CampaignCatalogReadModel>();
        services.AddScoped<ICampaignManagementReadModel, CampaignManagementReadModel>();
        services.AddScoped<ICampaignApplicationReadModel, CampaignApplicationReadModel>();
        services.AddScoped<IBrandFaceCatalogReadModel, BrandFaceCatalogReadModel>();
        services.AddScoped<IFavoritesReadModel, FavoritesReadModel>();
        services.AddScoped<IBrandFaceFavoritesReadModel, BrandFaceFavoritesReadModel>();
        services.AddScoped<IAdminMarketplaceReadModel, AdminMarketplaceReadModel>();
        services.AddScoped<IAdminAnalyticsReadModel, AdminAnalyticsReadModel>();
        services.AddScoped<IAdminComplaintReviewReadModel, AdminComplaintReviewReadModel>();
        services.AddScoped<IReviewReadModel, ReviewReadModel>();
        services.AddScoped<IPublicBusinessReadModel, PublicBusinessReadModel>();
        services.AddScoped<IDealReadModel, DealReadModel>();
        services.Configure<ProfileMediaOptions>(configuration.GetSection(ProfileMediaOptions.SectionName));
        services.AddSingleton<IProfileMediaStorage, S3ProfileMediaStorage>();
        services.AddSingleton<ICatalogCache, DistributedCatalogCache>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<BloggerBazarDbContext>());
        services.Configure<TelegramOptions>(configuration.GetSection(TelegramOptions.SectionName));
        services.Configure<AdministrationOptions>(configuration.GetSection(AdministrationOptions.SectionName));
        services.Configure<AdminWebOptions>(configuration.GetSection(AdminWebOptions.SectionName));
        services.AddSingleton<IConfiguredOwners, ConfiguredOwners>();
        services.AddSingleton<ITelegramWebAppValidator, TelegramWebAppValidator>();
        services.AddSingleton<ITelegramLoginWidgetValidator, TelegramLoginWidgetValidator>();
        services.AddSingleton<IAdminWebSessionStore, DistributedAdminWebSessionStore>();
        services.AddSingleton<ITelegramWebhookValidator, TelegramWebhookValidator>();
        services.AddScoped<IRecipientLanguageLookup, RecipientLanguageLookup>();
        services.AddHttpClient<ITelegramBotClient, TelegramBotClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.telegram.org/");
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        services.AddScoped<IAdminAccessPolicy, ConfiguredAdminAccessPolicy>();
        services.AddScoped<IPlatformUserAccessPolicy, PlatformUserAccessPolicy>();

        var redisConnection = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddDistributedMemoryCache();
        }
        else
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
        }

        return services;
    }
}
