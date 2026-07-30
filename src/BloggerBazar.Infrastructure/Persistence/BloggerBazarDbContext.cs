using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BloggerBazar.Infrastructure.Persistence;

public sealed class BloggerBazarDbContext(DbContextOptions<BloggerBazarDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<BloggerProfile> BloggerProfiles => Set<BloggerProfile>();
    public DbSet<BusinessProfile> BusinessProfiles => Set<BusinessProfile>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignApplication> CampaignApplications => Set<CampaignApplication>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();
    public DbSet<ContactUnlock> ContactUnlocks => Set<ContactUnlock>();
    public DbSet<PortfolioItem> PortfolioItems => Set<PortfolioItem>();
    public DbSet<SocialPlatform> SocialPlatforms => Set<SocialPlatform>();
    public DbSet<CollaborationRequest> CollaborationRequests => Set<CollaborationRequest>();
    public DbSet<CreditAccount> CreditAccounts => Set<CreditAccount>();
    public DbSet<CreditLedgerEntry> CreditLedgerEntries => Set<CreditLedgerEntry>();
    public DbSet<PlatformUser> PlatformUsers => Set<PlatformUser>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<BrandFaceProfile> BrandFaceProfiles => Set<BrandFaceProfile>();

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            ChangeTracker.Clear();
            return false;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<PlatformUser>();
        user.ToTable("platform_users");
        user.HasKey(entity => entity.Id);
        user.Property(entity => entity.FirstName).HasMaxLength(128).IsRequired();
        user.Property(entity => entity.Username).HasMaxLength(64);
        user.Property(entity => entity.Role).HasConversion<int>();
        user.Property(entity => entity.SelectedMarketplaceRole).HasConversion<int?>();
        user.HasIndex(entity => entity.TelegramUserId).IsUnique();
        user.HasIndex(entity => new { entity.Role, entity.IsBlocked, entity.IsDeleted });

        var auditLog = modelBuilder.Entity<AuditLog>();
        auditLog.ToTable("audit_logs");
        auditLog.HasKey(entity => entity.Id);
        auditLog.Property(entity => entity.Action).HasMaxLength(100).IsRequired();
        auditLog.Property(entity => entity.TargetType).HasMaxLength(100).IsRequired();
        auditLog.Property(entity => entity.TargetId).HasMaxLength(128).IsRequired();
        auditLog.Property(entity => entity.Details).HasMaxLength(2000);
        auditLog.HasIndex(entity => new { entity.ActorTelegramUserId, entity.CreatedAtUtc });
        auditLog.HasIndex(entity => new { entity.TargetType, entity.TargetId, entity.CreatedAtUtc });

        var brandFace = modelBuilder.Entity<BrandFaceProfile>();
        brandFace.ToTable("brand_face_profiles");
        brandFace.HasKey(entity => entity.Id);
        brandFace.Property(entity => entity.Name).HasMaxLength(100).IsRequired();
        brandFace.Property(entity => entity.City).HasMaxLength(80).IsRequired();
        brandFace.Property(entity => entity.Gender).HasMaxLength(32);
        brandFace.Property(entity => entity.Experience).HasMaxLength(2000);
        brandFace.Property(entity => entity.Instagram).HasMaxLength(128);
        brandFace.Property(entity => entity.Telegram).HasMaxLength(128);
        brandFace.Property(entity => entity.PortfolioUrl).HasMaxLength(2048);
        brandFace.Property(entity => entity.Description).HasMaxLength(2000);
        brandFace.Property(entity => entity.AvatarUrl).HasMaxLength(2048);
        brandFace.Property(entity => entity.Languages).HasColumnType("text[]");
        brandFace.Property(entity => entity.Categories).HasColumnType("text[]");
        brandFace.HasIndex(entity => entity.TelegramUserId).IsUnique();
        brandFace.HasIndex(entity => new { entity.City, entity.IsPromoted });

        var profile = modelBuilder.Entity<BloggerProfile>();
        profile.ToTable("blogger_profiles");
        profile.HasKey(entity => entity.Id);
        profile.Property(entity => entity.TelegramUserId).HasColumnName("telegram_user_id");
        profile.Ignore(entity => entity.CreatorLevel);
        profile.HasIndex(entity => entity.TelegramUserId).IsUnique();
        profile.Property(entity => entity.Name).HasMaxLength(100).IsRequired();
        profile.Property(entity => entity.LastName).HasMaxLength(100);
        profile.Property(entity => entity.Username).HasMaxLength(64);
        profile.Property(entity => entity.City).HasMaxLength(80).IsRequired();
        profile.Property(entity => entity.Categories).HasColumnType("text[]");
        profile.Property(entity => entity.Bio).HasMaxLength(500);
        profile.Property(entity => entity.AvatarUrl).HasMaxLength(2048);
        profile.Property(entity => entity.CoverUrl).HasMaxLength(2048);
        profile.Property(entity => entity.Phone).HasMaxLength(20);
        profile.Property(entity => entity.Email).HasMaxLength(254);
        profile.Property(entity => entity.Gender).HasMaxLength(32);
        profile.Property(entity => entity.Language).HasMaxLength(16);
        profile.Property(entity => entity.Subcategory).HasMaxLength(100);
        profile.Property(entity => entity.PriceNote).HasMaxLength(500);
        profile.Property(entity => entity.EngagementRate).HasPrecision(5, 2);
        profile.HasIndex(entity => new { entity.Status, entity.City });
        profile.HasIndex(entity => entity.IsPromoted);

        var portfolioItem = modelBuilder.Entity<PortfolioItem>();
        portfolioItem.ToTable("portfolio_items");
        portfolioItem.HasKey(entity => entity.Id);
        portfolioItem.Property(entity => entity.Title).HasMaxLength(120).IsRequired();
        portfolioItem.Property(entity => entity.Url).HasMaxLength(2048).IsRequired();
        portfolioItem.HasOne(entity => entity.Blogger).WithMany(entity => entity.PortfolioItems).HasForeignKey(entity => entity.BloggerId).OnDelete(DeleteBehavior.Cascade);
        portfolioItem.HasIndex(entity => new { entity.BloggerId, entity.CreatedAtUtc });

        var platform = modelBuilder.Entity<SocialPlatform>();
        platform.ToTable("social_platforms");
        platform.HasKey(entity => entity.Id);
        platform.Property(entity => entity.Type).HasMaxLength(50).IsRequired();
        platform.Property(entity => entity.Url).HasMaxLength(2048).IsRequired();
        platform.Property(entity => entity.ScreenshotUrl).HasMaxLength(2048);
        platform.HasOne(entity => entity.Blogger).WithMany(entity => entity.Platforms).HasForeignKey(entity => entity.BloggerId).OnDelete(DeleteBehavior.Cascade);
        platform.HasIndex(entity => entity.BloggerId);

        var business = modelBuilder.Entity<BusinessProfile>();
        business.ToTable("business_profiles");
        business.HasKey(entity => entity.Id);
        business.Property(entity => entity.TelegramUserId).HasColumnName("telegram_user_id");
        business.HasIndex(entity => entity.TelegramUserId).IsUnique();
        business.Property(entity => entity.Name).HasMaxLength(150).IsRequired();
        business.Property(entity => entity.Username).HasMaxLength(64);
        business.Property(entity => entity.City).HasMaxLength(80);
        business.Property(entity => entity.LogoUrl).HasMaxLength(2048);
        business.Property(entity => entity.WebsiteUrl).HasMaxLength(2048);
        business.Property(entity => entity.Description).HasMaxLength(1000);
        business.Property(entity => entity.Phone).HasMaxLength(20);
        business.Property(entity => entity.Email).HasMaxLength(254);
        business.Property(entity => entity.ModerationStatus).HasConversion<int>();
        business.HasIndex(entity => entity.ModerationStatus);

        var campaign = modelBuilder.Entity<Campaign>();
        campaign.ToTable("campaigns");
        campaign.HasKey(entity => entity.Id);
        campaign.Property(entity => entity.Title).HasMaxLength(160).IsRequired();
        campaign.Property(entity => entity.Description).HasMaxLength(3000).IsRequired();
        campaign.Property(entity => entity.City).HasMaxLength(80);
        campaign.Property(entity => entity.Categories).HasColumnType("text[]");
        campaign.Property(entity => entity.Requirements).HasColumnType("text[]");
        campaign.Property(entity => entity.Deadline);
        campaign.HasOne(entity => entity.Business).WithMany(entity => entity.Campaigns).HasForeignKey(entity => entity.BusinessId).OnDelete(DeleteBehavior.Cascade);
        campaign.HasIndex(entity => new { entity.Status, entity.IsPromoted });

        var application = modelBuilder.Entity<CampaignApplication>();
        application.ToTable("campaign_applications");
        application.HasKey(entity => entity.Id);
        application.Property(entity => entity.Message).HasMaxLength(1000);
        application.HasOne(entity => entity.Campaign).WithMany(entity => entity.Applications).HasForeignKey(entity => entity.CampaignId).OnDelete(DeleteBehavior.Cascade);
        application.HasOne(entity => entity.Blogger).WithMany(entity => entity.CampaignApplications).HasForeignKey(entity => entity.BloggerId).OnDelete(DeleteBehavior.Cascade);
        application.HasIndex(entity => new { entity.CampaignId, entity.BloggerId }).IsUnique();
        application.HasIndex(entity => new { entity.BloggerId, entity.Status });

        var collaborationRequest = modelBuilder.Entity<CollaborationRequest>();
        collaborationRequest.ToTable("collaboration_requests");
        collaborationRequest.HasKey(entity => entity.Id);
        collaborationRequest.Property(entity => entity.Message).HasMaxLength(1000).IsRequired();
        collaborationRequest.HasOne(entity => entity.Blogger).WithMany(entity => entity.IncomingRequests).HasForeignKey(entity => entity.BloggerId).OnDelete(DeleteBehavior.Cascade);
        collaborationRequest.HasOne(entity => entity.Business).WithMany(entity => entity.CollaborationRequests).HasForeignKey(entity => entity.BusinessId).OnDelete(DeleteBehavior.Cascade);
        collaborationRequest.HasIndex(entity => new { entity.BloggerId, entity.Status });
        collaborationRequest.HasIndex(entity => new { entity.BusinessId, entity.Status });

        var deal = modelBuilder.Entity<Deal>();
        deal.ToTable("deals");
        deal.HasKey(entity => entity.Id);
        deal.HasOne(entity => entity.CampaignApplication).WithOne(entity => entity.Deal).HasForeignKey<Deal>(entity => entity.CampaignApplicationId).OnDelete(DeleteBehavior.Cascade);
        deal.HasOne(entity => entity.CollaborationRequest).WithOne(entity => entity.Deal).HasForeignKey<Deal>(entity => entity.CollaborationRequestId).OnDelete(DeleteBehavior.Cascade);
        deal.HasIndex(entity => entity.CampaignApplicationId).IsUnique().HasFilter("\"CampaignApplicationId\" IS NOT NULL");
        deal.HasIndex(entity => entity.CollaborationRequestId).IsUnique().HasFilter("\"CollaborationRequestId\" IS NOT NULL");
        deal.HasOne(entity => entity.Blogger).WithMany(entity => entity.Deals).HasForeignKey(entity => entity.BloggerId).OnDelete(DeleteBehavior.Restrict);
        deal.HasOne(entity => entity.Business).WithMany(entity => entity.Deals).HasForeignKey(entity => entity.BusinessId).OnDelete(DeleteBehavior.Restrict);
        deal.HasIndex(entity => new { entity.BloggerId, entity.Status });
        deal.HasIndex(entity => new { entity.BusinessId, entity.Status });

        var review = modelBuilder.Entity<Review>();
        review.ToTable("reviews");
        review.HasKey(entity => entity.Id);
        review.Property(entity => entity.Comment).HasMaxLength(1000);
        review.HasOne(entity => entity.Deal).WithMany(entity => entity.Reviews).HasForeignKey(entity => entity.DealId).OnDelete(DeleteBehavior.Cascade);
        review.HasOne(entity => entity.Blogger).WithMany(entity => entity.Reviews).HasForeignKey(entity => entity.BloggerId).OnDelete(DeleteBehavior.Restrict);
        review.HasOne(entity => entity.Business).WithMany(entity => entity.Reviews).HasForeignKey(entity => entity.BusinessId).OnDelete(DeleteBehavior.Restrict);
        review.HasIndex(entity => new { entity.DealId, entity.ReviewerTelegramUserId }).IsUnique();
        review.HasIndex(entity => entity.BloggerId);
        review.HasIndex(entity => entity.BusinessId);

        var paymentOrder = modelBuilder.Entity<PaymentOrder>();
        paymentOrder.ToTable("payment_orders");
        paymentOrder.HasKey(entity => entity.Id);
        paymentOrder.Property(entity => entity.Reference).HasMaxLength(80).IsRequired();
        paymentOrder.HasIndex(entity => entity.Reference).IsUnique();
        paymentOrder.Property(entity => entity.ProviderTransactionId).HasMaxLength(120);
        paymentOrder.HasIndex(entity => new { entity.Status, entity.ExpiresAtUtc });
        paymentOrder.HasIndex(entity => entity.ProviderTransactionId).IsUnique().HasFilter("\"ProviderTransactionId\" IS NOT NULL");
        paymentOrder.HasIndex(entity => new { entity.PayerTelegramUserId, entity.Status });
        paymentOrder.HasIndex(entity => new { entity.PayerTelegramUserId, entity.TargetType, entity.TargetId })
            .HasDatabaseName("IX_payment_orders_pending_contact_unlock")
            .IsUnique()
            .HasFilter("\"Status\" = 0");

        var contactUnlock = modelBuilder.Entity<ContactUnlock>();
        contactUnlock.ToTable("contact_unlocks");
        contactUnlock.HasKey(entity => entity.Id);
        contactUnlock.HasOne(entity => entity.PaymentOrder).WithOne(entity => entity.ContactUnlock).HasForeignKey<ContactUnlock>(entity => entity.PaymentOrderId).OnDelete(DeleteBehavior.Restrict);
        contactUnlock.HasIndex(entity => entity.PaymentOrderId).IsUnique();
        contactUnlock.HasIndex(entity => new { entity.ViewerTelegramUserId, entity.TargetType, entity.TargetId }).IsUnique();

        var creditAccount = modelBuilder.Entity<CreditAccount>();
        creditAccount.ToTable("credit_accounts");
        creditAccount.HasKey(entity => entity.Id);
        creditAccount.HasIndex(entity => entity.TelegramUserId).IsUnique();

        var creditLedgerEntry = modelBuilder.Entity<CreditLedgerEntry>();
        creditLedgerEntry.ToTable("credit_ledger_entries");
        creditLedgerEntry.HasKey(entity => entity.Id);
        creditLedgerEntry.Property(entity => entity.Note).HasMaxLength(200);
        creditLedgerEntry.HasOne(entity => entity.Account).WithMany(entity => entity.LedgerEntries).HasForeignKey(entity => entity.TelegramUserId).HasPrincipalKey(entity => entity.TelegramUserId).OnDelete(DeleteBehavior.Cascade);
        creditLedgerEntry.HasIndex(entity => entity.TelegramUserId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => base.SaveChangesAsync(cancellationToken);
}
