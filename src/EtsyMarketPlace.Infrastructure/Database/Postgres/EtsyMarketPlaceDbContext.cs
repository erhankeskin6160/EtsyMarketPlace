using EtsyMarketPlace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EtsyMarketPlace.Infrastructure.Database.Postgres;

/// <summary>
/// PostgreSQL tabanlı EtsyMarketPlace Entity Framework Core Veritabanı Bağlamı (DbContext).
/// </summary>
public class EtsyMarketPlaceDbContext : DbContext
{
    public EtsyMarketPlaceDbContext(DbContextOptions<EtsyMarketPlaceDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ShopConnection> ShopConnections => Set<ShopConnection>();
    public DbSet<OAuthToken> OAuthTokens => Set<OAuthToken>();
    public DbSet<EtsyAppCredential> EtsyAppCredentials => Set<EtsyAppCredential>();
    public DbSet<SyncState> SyncStates => Set<SyncState>();
    public DbSet<BankPayout> BankPayouts => Set<BankPayout>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<OrderCost> OrderCosts => Set<OrderCost>();
    public DbSet<MonthlyOrderSummary> MonthlyOrderSummaries => Set<MonthlyOrderSummary>();
    public DbSet<ListingTrafficDaily> ListingTrafficDailies => Set<ListingTrafficDaily>();
    public DbSet<ListingAiAudit> ListingAiAudits => Set<ListingAiAudit>();
    public DbSet<ShopTelegramSetting> ShopTelegramSettings => Set<ShopTelegramSetting>();
    public DbSet<ShopCarrierSession> ShopCarrierSessions => Set<ShopCarrierSession>();
    public DbSet<ShopAiSetting> ShopAiSettings => Set<ShopAiSetting>();
    public DbSet<ChartSnapshot> ChartSnapshots => Set<ChartSnapshot>();
    public DbSet<OrderAddress> OrderAddresses => Set<OrderAddress>();
    public DbSet<ListingAbTest> ListingAbTests => Set<ListingAbTest>();
    public DbSet<BatchQueueItemEntity> BatchQueueItems => Set<BatchQueueItemEntity>();
    public DbSet<AiUsageRecordEntity> AiUsageHistory => Set<AiUsageRecordEntity>();
    public DbSet<TrackingItemEntity> TrackingItems => Set<TrackingItemEntity>();
    public DbSet<TrackingSnapshotEntity> TrackingSnapshots => Set<TrackingSnapshotEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. AppUser
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("app_users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(128);
            entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.Property(e => e.PasswordSalt).HasColumnName("password_salt").IsRequired();
            entity.Property(e => e.Role).HasColumnName("role").HasMaxLength(50).HasDefaultValue("StoreOwner");
            entity.Property(e => e.AssignedShopIds).HasColumnName("assigned_shop_ids").HasColumnType("text").HasDefaultValue("[]");
            entity.Property(e => e.MonthlyAiTokenQuota).HasColumnName("monthly_ai_token_quota").HasDefaultValue(500000);
            entity.Property(e => e.UsedAiTokens).HasColumnName("used_ai_tokens").HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at").HasColumnType("timestamp with time zone");

            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // 2. AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.UserId).HasColumnName("user_id").HasMaxLength(128).IsRequired();
            entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Details).HasColumnName("details").HasColumnType("text");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
            entity.Property(e => e.Timestamp).HasColumnName("timestamp").HasColumnType("timestamp with time zone");

            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.UserId);
        });

        // 3. ShopConnection
        modelBuilder.Entity<ShopConnection>(entity =>
        {
            entity.ToTable("shop_connections");
            entity.HasKey(e => e.ShopId);
            entity.Property(e => e.ShopId).HasColumnName("shop_id").HasMaxLength(64);
            entity.Property(e => e.ShopName).HasColumnName("shop_name").HasMaxLength(255).HasDefaultValue(string.Empty);
            entity.Property(e => e.UserId).HasColumnName("user_id").HasMaxLength(128).HasDefaultValue(string.Empty);
            entity.Property(e => e.LastSynchronizedAt).HasColumnName("last_synchronized_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        });

        // 4. OAuthToken
        modelBuilder.Entity<OAuthToken>(entity =>
        {
            entity.ToTable("oauth_tokens");
            entity.HasKey(e => e.ShopId);
            entity.Property(e => e.ShopId).HasColumnName("shop_id").HasMaxLength(64);
            entity.Property(e => e.AccessToken).HasColumnName("access_token").HasColumnType("text").IsRequired();
            entity.Property(e => e.RefreshToken).HasColumnName("refresh_token").HasColumnType("text").IsRequired();
            entity.Property(e => e.AccessTokenExpiresAt).HasColumnName("access_token_expires_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.TokenType).HasColumnName("token_type").HasMaxLength(50).HasDefaultValue("Bearer");

            entity.HasOne(e => e.Shop)
                .WithOne(s => s.OAuthToken)
                .HasForeignKey<OAuthToken>(e => e.ShopId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 5. EtsyAppCredential
        modelBuilder.Entity<EtsyAppCredential>(entity =>
        {
            entity.ToTable("etsy_app_credentials");
            entity.HasKey(e => e.ShopId);
            entity.Property(e => e.ShopId).HasColumnName("shop_id").HasMaxLength(64);
            entity.Property(e => e.EncryptedKeystring).HasColumnName("encrypted_keystring").HasColumnType("text");
            entity.Property(e => e.EncryptedSharedSecret).HasColumnName("encrypted_shared_secret").HasColumnType("text");
            entity.Property(e => e.RedirectUri).HasColumnName("redirect_uri").HasMaxLength(500);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

            entity.HasOne(e => e.Shop)
                .WithOne(s => s.AppCredential)
                .HasForeignKey<EtsyAppCredential>(e => e.ShopId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 6. SyncState
        modelBuilder.Entity<SyncState>(entity =>
        {
            entity.ToTable("sync_states");
            entity.HasKey(e => new { e.ShopId, e.DataType });
            entity.Property(e => e.ShopId).HasColumnName("shop_id").HasMaxLength(64);
            entity.Property(e => e.DataType).HasColumnName("data_type").HasMaxLength(64);
            entity.Property(e => e.LastSuccessfulSyncAt).HasColumnName("last_successful_sync_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.LastCursorAt).HasColumnName("last_cursor_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.LastError).HasColumnName("last_error").HasColumnType("text");

            entity.HasOne(e => e.Shop)
                .WithMany(s => s.SyncStates)
                .HasForeignKey(e => e.ShopId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 7. BankPayout
        modelBuilder.Entity<BankPayout>(entity =>
        {
            entity.ToTable("bank_payouts");
            entity.HasKey(e => new { e.ShopId, e.ReferenceId });
            entity.Property(e => e.ShopId).HasColumnName("shop_id").HasMaxLength(64);
            entity.Property(e => e.ReferenceId).HasColumnName("reference_id").HasMaxLength(128);
            entity.Property(e => e.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.Amount).HasColumnName("amount").HasPrecision(18, 4);
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(10).HasDefaultValue("USD");
            entity.Property(e => e.ExchangeRateToTry).HasColumnName("exchange_rate_to_try").HasPrecision(18, 4);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);

            entity.HasIndex(e => new { e.ShopId, e.OccurredAt });

            entity.HasOne(e => e.Shop)
                .WithMany(s => s.BankPayouts)
                .HasForeignKey(e => e.ShopId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 8. FinancialTransaction
        modelBuilder.Entity<FinancialTransaction>(entity =>
        {
            entity.ToTable("financial_transactions");
            entity.HasKey(e => new { e.ShopId, e.ReferenceId });
            entity.Property(e => e.ShopId).HasColumnName("shop_id").HasMaxLength(64);
            entity.Property(e => e.ReferenceId).HasColumnName("reference_id").HasMaxLength(128);
            entity.Property(e => e.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.GrossSales).HasColumnName("gross_sales").HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(e => e.PlatformFees).HasColumnName("platform_fees").HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(e => e.InternalAdsCost).HasColumnName("internal_ads_cost").HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(e => e.ExternalAdsCost).HasColumnName("external_ads_cost").HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(e => e.ProductCost).HasColumnName("product_cost").HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(e => e.ShippingCost).HasColumnName("shipping_cost").HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(e => e.Refunds).HasColumnName("refunds").HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(10).HasDefaultValue("USD");

            entity.HasIndex(e => new { e.ShopId, e.OccurredAt });

            entity.HasOne(e => e.Shop)
                .WithMany(s => s.FinancialTransactions)
                .HasForeignKey(e => e.ShopId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 9. OrderCost
        modelBuilder.Entity<OrderCost>(entity =>
        {
            entity.ToTable("order_costs");
            entity.HasKey(e => new { e.ShopId, e.OrderId });
            entity.Property(e => e.ShopId).HasColumnName("shop_id").HasMaxLength(64);
            entity.Property(e => e.OrderId).HasColumnName("order_id").HasMaxLength(128);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(10).HasDefaultValue("USD");
            entity.Property(e => e.OrderTotal).HasColumnName("order_total").HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(e => e.ProductCost).HasColumnName("product_cost").HasPrecision(18, 4);
            entity.Property(e => e.ShippingCost).HasColumnName("shipping_cost").HasPrecision(18, 4);
            entity.Property(e => e.AlertReason).HasColumnName("alert_reason").HasMaxLength(500);

            entity.HasIndex(e => new { e.ShopId, e.CreatedAt });

            entity.HasOne(e => e.Shop)
                .WithMany(s => s.OrderCosts)
                .HasForeignKey(e => e.ShopId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 10. MonthlyOrderSummary
        modelBuilder.Entity<MonthlyOrderSummary>(entity =>
        {
            entity.ToTable("monthly_order_summaries");
            entity.HasKey(e => new { e.ShopId, e.YearMonth });
            entity.Property(e => e.ShopId).HasColumnName("shop_id").HasMaxLength(64);
            entity.Property(e => e.YearMonth).HasColumnName("year_month").HasMaxLength(7);
            entity.Property(e => e.OrderCount).HasColumnName("order_count").HasDefaultValue(0);
            entity.Property(e => e.UnitsSold).HasColumnName("units_sold").HasDefaultValue(0);
            entity.Property(e => e.GrossRevenue).HasColumnName("gross_revenue").HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(e => e.AvgOrderValue).HasColumnName("avg_order_value").HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(e => e.ShippedOrders).HasColumnName("shipped_orders").HasDefaultValue(0);
            entity.Property(e => e.UnfulfilledOrders).HasColumnName("unfulfilled_orders").HasDefaultValue(0);
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(10).HasDefaultValue("USD");

            entity.HasOne(e => e.Shop)
                .WithMany(s => s.MonthlyOrderSummaries)
                .HasForeignKey(e => e.ShopId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 11. ListingTrafficDaily
        modelBuilder.Entity<ListingTrafficDaily>(entity =>
        {
            entity.ToTable("listing_traffic_daily");
            entity.HasKey(e => new { e.ShopId, e.ListingId, e.SnapshotDate });
            entity.Property(e => e.ShopId).HasColumnName("shop_id").HasMaxLength(64);
            entity.Property(e => e.ListingId).HasColumnName("listing_id");
            entity.Property(e => e.SnapshotDate).HasColumnName("snapshot_date").HasMaxLength(10);
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(500);
            entity.Property(e => e.Views).HasColumnName("views").HasDefaultValue(0);
            entity.Property(e => e.Favorites).HasColumnName("favorites").HasDefaultValue(0);
            entity.Property(e => e.ViewsToday).HasColumnName("views_today").HasDefaultValue(0);
            entity.Property(e => e.FavoritesToday).HasColumnName("favorites_today").HasDefaultValue(0);
            entity.Property(e => e.UnitsSoldMonth).HasColumnName("units_sold_month").HasDefaultValue(0);
            entity.Property(e => e.RevenueMonth).HasColumnName("revenue_month").HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(e => e.ConversionRate).HasColumnName("conversion_rate").HasPrecision(8, 4).HasDefaultValue(0m);
            entity.Property(e => e.ImageUrl).HasColumnName("image_url").HasMaxLength(1000);
            entity.Property(e => e.ListingUrl).HasColumnName("listing_url").HasMaxLength(1000);

            entity.HasOne(e => e.Shop)
                .WithMany(s => s.ListingTrafficDailies)
                .HasForeignKey(e => e.ShopId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 12. ListingAiAudit
        modelBuilder.Entity<ListingAiAudit>(entity =>
        {
            entity.ToTable("listing_ai_audits");
            entity.HasKey(e => new { e.ShopId, e.ListingId });
            entity.Property(e => e.ShopId).HasColumnName("shop_id").HasMaxLength(64);
            entity.Property(e => e.ListingId).HasColumnName("listing_id").HasMaxLength(64);
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(500);
            entity.Property(e => e.CurrentSeoScore).HasColumnName("current_seo_score").HasDefaultValue(0);
            entity.Property(e => e.OptimizedSeoScore).HasColumnName("optimized_seo_score").HasDefaultValue(0);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(e => e.Provider).HasColumnName("provider").HasMaxLength(50);
            entity.Property(e => e.Model).HasColumnName("model").HasMaxLength(50);
            entity.Property(e => e.ResultJson).HasColumnName("result_json").HasColumnType("text").HasDefaultValue("{}");
            entity.Property(e => e.AuditedAt).HasColumnName("audited_at").HasColumnType("timestamp with time zone");

            entity.HasOne(e => e.Shop)
                .WithMany(s => s.ListingAiAudits)
                .HasForeignKey(e => e.ShopId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 13. ShopTelegramSetting
        modelBuilder.Entity<ShopTelegramSetting>(entity =>
        {
            entity.ToTable("shop_telegram_settings");
            entity.HasKey(e => e.ShopId);
            entity.Property(e => e.ShopId).HasColumnName("shop_id").HasMaxLength(64);
            entity.Property(e => e.EncryptedBotToken).HasColumnName("encrypted_bot_token").HasColumnType("text");
            entity.Property(e => e.ChatId).HasColumnName("chat_id").HasMaxLength(100);
            entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(false);
            entity.Property(e => e.NotifyOnOrders).HasColumnName("notify_on_orders").HasDefaultValue(true);
            entity.Property(e => e.NotifyOnStock).HasColumnName("notify_on_stock").HasDefaultValue(true);
            entity.Property(e => e.DailyBriefEnabled).HasColumnName("daily_brief_enabled").HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

            entity.HasOne(e => e.Shop)
                .WithOne(s => s.TelegramSetting)
                .HasForeignKey<ShopTelegramSetting>(e => e.ShopId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 14. ShopCarrierSession
        modelBuilder.Entity<ShopCarrierSession>(entity =>
        {
            entity.ToTable("shop_carrier_sessions");
            entity.HasKey(e => new { e.ShopId, e.CarrierId });
            entity.Property(e => e.ShopId).HasColumnName("shop_id").HasMaxLength(64);
            entity.Property(e => e.CarrierId).HasColumnName("carrier_id").HasMaxLength(50);
            entity.Property(e => e.EncryptedCredentials).HasColumnName("encrypted_credentials").HasColumnType("text");
            entity.Property(e => e.AccountNo).HasColumnName("account_no").HasMaxLength(100);
            entity.Property(e => e.ServiceLevel).HasColumnName("service_level").HasMaxLength(100);
            entity.Property(e => e.IsConnected).HasColumnName("is_connected").HasDefaultValue(false);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

            entity.HasOne(e => e.Shop)
                .WithMany(s => s.CarrierSessions)
                .HasForeignKey(e => e.ShopId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 15. ShopAiSetting
        modelBuilder.Entity<ShopAiSetting>(entity =>
        {
            entity.ToTable("shop_ai_settings");
            entity.HasKey(e => e.ShopId);
            entity.Property(e => e.ShopId).HasColumnName("shop_id").HasMaxLength(64);
            entity.Property(e => e.SettingsJson).HasColumnName("settings_json").HasColumnType("text").HasDefaultValue("{}");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

            entity.HasOne(e => e.Shop)
                .WithOne(s => s.AiSetting)
                .HasForeignKey<ShopAiSetting>(e => e.ShopId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 16. ChartSnapshot
        modelBuilder.Entity<ChartSnapshot>(entity =>
        {
            entity.ToTable("chart_snapshots");
            entity.HasKey(e => new { e.ShopId, e.ChartType });
            entity.Property(e => e.ShopId).HasColumnName("shop_id").HasMaxLength(64);
            entity.Property(e => e.ChartType).HasColumnName("chart_type").HasMaxLength(50);
            entity.Property(e => e.PeriodStart).HasColumnName("period_start").HasMaxLength(30);
            entity.Property(e => e.PeriodEnd).HasColumnName("period_end").HasMaxLength(30);
            entity.Property(e => e.ImagePngBase64).HasColumnName("image_png_base64").HasColumnType("text");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

            entity.HasOne(e => e.Shop)
                .WithMany()
                .HasForeignKey(e => e.ShopId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 17. OrderAddress
        modelBuilder.Entity<OrderAddress>(entity =>
        {
            entity.ToTable("order_addresses");
            entity.HasKey(e => e.ReceiptId);
            entity.Property(e => e.ReceiptId).HasColumnName("receipt_id");
            entity.Property(e => e.BuyerName).HasColumnName("buyer_name").HasMaxLength(255).HasDefaultValue(string.Empty);
            entity.Property(e => e.BuyerEmail).HasColumnName("buyer_email").HasMaxLength(255).HasDefaultValue(string.Empty);
            entity.Property(e => e.Phone).HasColumnName("phone").HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(e => e.StreetAddress).HasColumnName("street_address").HasColumnType("text").HasDefaultValue(string.Empty);
            entity.Property(e => e.SecondAddress).HasColumnName("second_address").HasColumnType("text").HasDefaultValue(string.Empty);
            entity.Property(e => e.City).HasColumnName("city").HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(e => e.State).HasColumnName("state").HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(e => e.PostalCode).HasColumnName("postal_code").HasMaxLength(50).HasDefaultValue(string.Empty);
            entity.Property(e => e.CountryCode).HasColumnName("country_code").HasMaxLength(10).HasDefaultValue(string.Empty);
            entity.Property(e => e.CountryName).HasColumnName("country_name").HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(e => e.Source).HasColumnName("source").HasMaxLength(50).HasDefaultValue(string.Empty);
            entity.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");

            entity.Property(e => e.IossNumber).HasColumnName("ioss_number").HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(e => e.ProductCostUsd).HasColumnName("product_cost_usd").HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(e => e.ShippingCostUsd).HasColumnName("shipping_cost_usd").HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(e => e.WeightKg).HasColumnName("weight_kg").HasDefaultValue(0.0);
            entity.Property(e => e.WidthCm).HasColumnName("width_cm").HasDefaultValue(0.0);
            entity.Property(e => e.LengthCm).HasColumnName("length_cm").HasDefaultValue(0.0);
            entity.Property(e => e.HeightCm).HasColumnName("height_cm").HasDefaultValue(0.0);
            entity.Property(e => e.GtipCode).HasColumnName("gtip_code").HasMaxLength(50).HasDefaultValue(string.Empty);
            entity.Property(e => e.SelectedCarrier).HasColumnName("selected_carrier").HasMaxLength(50).HasDefaultValue(string.Empty);
            entity.Property(e => e.CarrierServiceName).HasColumnName("carrier_service_name").HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(e => e.TrackingCode).HasColumnName("tracking_code").HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(e => e.LabelUrl).HasColumnName("label_url").HasColumnType("text").HasDefaultValue(string.Empty);

            entity.HasIndex(e => e.UpdatedAtUtc);
        });

        // 18. ListingAbTest
        modelBuilder.Entity<ListingAbTest>(entity =>
        {
            entity.ToTable("listing_ab_tests");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.ListingId).HasColumnName("listing_id").HasMaxLength(64).HasDefaultValue(string.Empty);
            entity.Property(e => e.ListingTitle).HasColumnName("listing_title").HasMaxLength(500).HasDefaultValue(string.Empty);
            entity.Property(e => e.ExperimentName).HasColumnName("experiment_name").HasMaxLength(255).HasDefaultValue(string.Empty);
            entity.Property(e => e.VariantATitle).HasColumnName("variant_a_title").HasColumnType("text").HasDefaultValue(string.Empty);
            entity.Property(e => e.VariantBTitle).HasColumnName("variant_b_title").HasColumnType("text").HasDefaultValue(string.Empty);
            entity.Property(e => e.VariantATags).HasColumnName("variant_a_tags").HasColumnType("text").HasDefaultValue("[]");
            entity.Property(e => e.VariantBTags).HasColumnName("variant_b_tags").HasColumnType("text").HasDefaultValue("[]");
            entity.Property(e => e.VariantADescription).HasColumnName("variant_a_description").HasColumnType("text").HasDefaultValue(string.Empty);
            entity.Property(e => e.VariantBDescription).HasColumnName("variant_b_description").HasColumnType("text").HasDefaultValue(string.Empty);
            entity.Property(e => e.StartDate).HasColumnName("start_date").HasColumnType("timestamp with time zone");
            entity.Property(e => e.EndDate).HasColumnName("end_date").HasColumnType("timestamp with time zone");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("Active");
            entity.Property(e => e.BeforeViews).HasColumnName("before_views").HasDefaultValue(0);
            entity.Property(e => e.BeforeFavorites).HasColumnName("before_favorites").HasDefaultValue(0);
            entity.Property(e => e.BeforeSales).HasColumnName("before_sales").HasDefaultValue(0);
            entity.Property(e => e.AfterViews).HasColumnName("after_views").HasDefaultValue(0);
            entity.Property(e => e.AfterFavorites).HasColumnName("after_favorites").HasDefaultValue(0);
            entity.Property(e => e.AfterSales).HasColumnName("after_sales").HasDefaultValue(0);

            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.ListingId);
        });

        // 19. BatchQueueItemEntity
        modelBuilder.Entity<BatchQueueItemEntity>(entity =>
        {
            entity.ToTable("batch_queue_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.ListingId).HasColumnName("listing_id").HasMaxLength(64).HasDefaultValue(string.Empty);
            entity.Property(e => e.OriginalTitle).HasColumnName("original_title").HasColumnType("text").HasDefaultValue(string.Empty);
            entity.Property(e => e.OriginalDescription).HasColumnName("original_description").HasColumnType("text").HasDefaultValue(string.Empty);
            entity.Property(e => e.OriginalTags).HasColumnName("original_tags").HasColumnType("text").HasDefaultValue("[]");
            entity.Property(e => e.TargetKeyword).HasColumnName("target_keyword").HasMaxLength(255).HasDefaultValue(string.Empty);
            entity.Property(e => e.Category).HasColumnName("category").HasMaxLength(255).HasDefaultValue(string.Empty);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("Pending");
            entity.Property(e => e.OverallScore).HasColumnName("overall_score").HasDefaultValue(0);
            entity.Property(e => e.OptimizedTitle).HasColumnName("optimized_title").HasColumnType("text").HasDefaultValue(string.Empty);
            entity.Property(e => e.OptimizedDescription).HasColumnName("optimized_description").HasColumnType("text").HasDefaultValue(string.Empty);
            entity.Property(e => e.OptimizedTags).HasColumnName("optimized_tags").HasColumnType("text").HasDefaultValue("[]");
            entity.Property(e => e.OptimizedMaterials).HasColumnName("optimized_materials").HasColumnType("text").HasDefaultValue("[]");
            entity.Property(e => e.RiskWarnings).HasColumnName("risk_warnings").HasColumnType("text").HasDefaultValue("[]");
            entity.Property(e => e.Issues).HasColumnName("issues").HasColumnType("text").HasDefaultValue("[]");
            entity.Property(e => e.ProcessedAt).HasColumnName("processed_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message").HasColumnType("text");
            entity.Property(e => e.IsSyncedToEtsy).HasColumnName("is_synced_to_etsy").HasDefaultValue(false);
            entity.Property(e => e.SyncedAt).HasColumnName("synced_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.AbTestExperimentId).HasColumnName("ab_test_experiment_id");
            entity.Property(e => e.AbTestStatus).HasColumnName("ab_test_status").HasMaxLength(50);

            entity.HasIndex(e => e.Status);
        });

        // 20. AiUsageRecordEntity
        modelBuilder.Entity<AiUsageRecordEntity>(entity =>
        {
            entity.ToTable("ai_usage_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.ModuleName).HasColumnName("module_name").HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(e => e.Provider).HasColumnName("provider").HasMaxLength(50).HasDefaultValue(string.Empty);
            entity.Property(e => e.ModelName).HasColumnName("model_name").HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(e => e.PromptTokens).HasColumnName("prompt_tokens").HasDefaultValue(0);
            entity.Property(e => e.CompletionTokens).HasColumnName("completion_tokens").HasDefaultValue(0);
            entity.Property(e => e.TotalTokens).HasColumnName("total_tokens").HasDefaultValue(0);
            entity.Property(e => e.CostUsd).HasColumnName("cost_usd").HasPrecision(18, 6).HasDefaultValue(0m);
            entity.Property(e => e.CostTry).HasColumnName("cost_try").HasPrecision(18, 6).HasDefaultValue(0m);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("Başarılı");
            entity.Property(e => e.Note).HasColumnName("note").HasColumnType("text");

            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Provider);
        });

        // 21. TrackingItemEntity & TrackingSnapshotEntity
        modelBuilder.Entity<TrackingItemEntity>(entity =>
        {
            entity.ToTable("tracking_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.EntityType).HasColumnName("entity_type");
            entity.Property(e => e.ExternalKey).HasColumnName("external_key").HasMaxLength(128).IsRequired();
            entity.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Url).HasColumnName("url").HasColumnType("text").HasDefaultValue(string.Empty);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");

            entity.HasIndex(e => new { e.EntityType, e.ExternalKey }).IsUnique();

            entity.HasMany(e => e.Snapshots)
                .WithOne(s => s.TrackingItem)
                .HasForeignKey(s => s.TrackingItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrackingSnapshotEntity>(entity =>
        {
            entity.ToTable("tracking_snapshots");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.TrackingItemId).HasColumnName("tracking_item_id");
            entity.Property(e => e.CapturedAt).HasColumnName("captured_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.Price).HasColumnName("price").HasPrecision(18, 4);
            entity.Property(e => e.CurrencyCode).HasColumnName("currency_code").HasMaxLength(10).HasDefaultValue(string.Empty);
            entity.Property(e => e.Favorites).HasColumnName("favorites");
            entity.Property(e => e.Views).HasColumnName("views");
            entity.Property(e => e.ShopSales).HasColumnName("shop_sales");
            entity.Property(e => e.ReviewCount).HasColumnName("review_count");
            entity.Property(e => e.ReviewAverage).HasColumnName("review_average").HasPrecision(5, 2);
            entity.Property(e => e.SeoScore).HasColumnName("seo_score");
            entity.Property(e => e.MarketScore).HasColumnName("market_score");
            entity.Property(e => e.DemandScore).HasColumnName("demand_score");
            entity.Property(e => e.CompetitionScore).HasColumnName("competition_score");
            entity.Property(e => e.OpportunityScore).HasColumnName("opportunity_score");

            entity.HasIndex(e => e.TrackingItemId);
        });
    }
}
