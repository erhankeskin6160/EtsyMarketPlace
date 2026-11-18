using System.Text.Json;
using EtsyMarketPlace.Domain.Subscriptions.Entities;

namespace EtsyMarketPlace.Domain.Subscriptions;

public static class SubscriptionPlanCode
{
    public const string Starter = "starter";
    public const string Growth = "growth";
    public const string Pro = "pro";
    public const string Enterprise = "enterprise";

    public static readonly IReadOnlyList<string> All =
    [
        Starter,
        Growth,
        Pro,
        Enterprise
    ];

    public static bool IsValid(string? planId)
        => !string.IsNullOrWhiteSpace(planId) &&
           All.Contains(planId.Trim().ToLowerInvariant(), StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string? planId)
    {
        if (string.IsNullOrWhiteSpace(planId)) return Starter;
        var normalized = planId.Trim().ToLowerInvariant();
        return All.Contains(normalized) ? normalized : Starter;
    }
}

public enum SubscriptionStatus
{
    Trialing = 0,
    Active = 1,
    PastDue = 2,
    Cancelled = 3,
    Expired = 4
}

public enum BillingCycle
{
    Monthly = 0,
    Yearly = 1
}

/// <summary>
/// Sistem modülleri için abonelik yetki anahtarları, hiyerarşi yardımcıları ve 4 standart SaaS paketi tohumları.
/// </summary>
public static class SubscriptionFeatureKeys
{
    // Starter
    public const string Dashboard = "dashboard";
    public const string OrdersShipping = "orders_shipping";
    public const string FastCreator = "fast_creator";
    public const string ProductDiscovery = "product_discovery";
    public const string ShopPerformance = "shop";
    public const string MarketResearch = "research";
    public const string ProfitCalculator = "profit";
    public const string LiveTracking = "tracking";
    public const string AiUsage = "ai_usage";
    public const string AiModels = "ai_models";
    public const string Notifications = "notifications";
    public const string EtsyApiSettings = "api";
    public const string SystemLogs = "logs";
    public const string ClientUpdate = "update";

    // Growth
    public const string CompetitorSpy = "competitor_spy";
    public const string ExternalArbitrage = "external";
    public const string AiAudit = "ai_audit";
    public const string FinancialAccounting = "financial";
    public const string FinancialAiAnalysis = "financial_ai";
    public const string Automation = "automation";
    public const string BatchQueue = "batch";
    public const string ShippingHub = "shipping_hub";

    // Pro
    public const string AiStudio = "ai_image";
    public const string Viral3D = "viral_3d";
    public const string AbTest = "ab_test";
    public const string ShopVault = "shop_vault";
    public const string DesktopClient = "desktop_client";

    // Enterprise
    public const string McpApi = "mcp_api";

    public static readonly IReadOnlyList<string> StarterFeatures =
    [
        Dashboard,
        OrdersShipping,
        FastCreator,
        ProductDiscovery,
        ShopPerformance,
        MarketResearch,
        ProfitCalculator,
        LiveTracking,
        AiUsage,
        AiModels,
        Notifications,
        EtsyApiSettings,
        SystemLogs,
        ClientUpdate
    ];

    public static readonly IReadOnlyList<string> GrowthFeatures =
    [
        .. StarterFeatures,
        CompetitorSpy,
        ExternalArbitrage,
        AiAudit,
        FinancialAccounting,
        FinancialAiAnalysis,
        Automation,
        BatchQueue,
        ShippingHub
    ];

    public static readonly IReadOnlyList<string> ProFeatures =
    [
        .. GrowthFeatures,
        AiStudio,
        Viral3D,
        AbTest,
        ShopVault,
        DesktopClient
    ];

    public static readonly IReadOnlyList<string> EnterpriseFeatures =
    [
        .. ProFeatures,
        McpApi
    ];

    public static string NormalizeFeatureKey(string? featureKey)
    {
        if (string.IsNullOrWhiteSpace(featureKey)) return string.Empty;
        var key = featureKey.Trim().ToLowerInvariant();
        return key switch
        {
            "aras" or "shipentegra" or "navlungo" or "shiptomore" or "shipping" => ShippingHub,
            "tdhp_accounting" or "accounting" => FinancialAccounting,
            "ai_studio" => AiStudio,
            "vault" => ShopVault,
            "orders" => OrdersShipping,
            _ => key
        };
    }

    public static string GetMinimumRequiredPlan(string? featureKey)
    {
        var normalized = NormalizeFeatureKey(featureKey);
        if (StarterFeatures.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            return SubscriptionPlanCode.Starter;
        if (GrowthFeatures.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            return SubscriptionPlanCode.Growth;
        if (ProFeatures.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            return SubscriptionPlanCode.Pro;
        if (EnterpriseFeatures.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            return SubscriptionPlanCode.Enterprise;
        return SubscriptionPlanCode.Starter;
    }

    public static IReadOnlyList<string> ParseAllowedFeatures(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static IReadOnlyList<SubscriptionPlan> GetDefaultPlans()
    {
        var now = DateTime.UtcNow;
        return
        [
            new SubscriptionPlan
            {
                Id = SubscriptionPlanCode.Starter,
                Name = "Starter",
                Tagline = "Yeni başlayan Etsy satıcıları için temel yapay zeka & mağaza kokpiti",
                BadgeText = "BAŞLANGIÇ",
                AccentColor = "#34d399",
                MonthlyPriceUsd = 19m,
                YearlyPriceUsd = 180m,
                MonthlyPriceTry = 650m,
                YearlyPriceTry = 6240m,
                MaxShops = 1,
                MonthlyAiTokenQuota = 100_000,
                MaxMonthlyAiListings = 15,
                MaxBatchQueueSize = 0,
                AllowedFeaturesJson = JsonSerializer.Serialize(StarterFeatures),
                IsPopular = false,
                IsActive = true,
                DisplayOrder = 1,
                UpdatedAtUtc = now
            },
            new SubscriptionPlan
            {
                Id = SubscriptionPlanCode.Growth,
                Name = "Growth",
                Tagline = "Büyüyen mağazalar için AI mağaza denetimi, 4 taşıyıcı kargo ve finans analitiği",
                BadgeText = "BÜYÜME",
                AccentColor = "#38bdf8",
                MonthlyPriceUsd = 49m,
                YearlyPriceUsd = 470m,
                MonthlyPriceTry = 1690m,
                YearlyPriceTry = 16200m,
                MaxShops = 2,
                MonthlyAiTokenQuota = 750_000,
                MaxMonthlyAiListings = 100,
                MaxBatchQueueSize = 10,
                AllowedFeaturesJson = JsonSerializer.Serialize(GrowthFeatures),
                IsPopular = false,
                IsActive = true,
                DisplayOrder = 2,
                UpdatedAtUtc = now
            },
            new SubscriptionPlan
            {
                Id = SubscriptionPlanCode.Pro,
                Name = "Pro",
                Tagline = "Profesyonel satıcılar & 3D stüdyolar için sınırsız AI, Görsel Stüdyo, ShopVault & Masaüstü",
                BadgeText = "EN POPÜLER",
                AccentColor = "#a855f7",
                MonthlyPriceUsd = 99m,
                YearlyPriceUsd = 950m,
                MonthlyPriceTry = 3450m,
                YearlyPriceTry = 33100m,
                MaxShops = 5,
                MonthlyAiTokenQuota = 2_500_000,
                MaxMonthlyAiListings = -1,
                MaxBatchQueueSize = 50,
                AllowedFeaturesJson = JsonSerializer.Serialize(ProFeatures),
                IsPopular = true,
                IsActive = true,
                DisplayOrder = 3,
                UpdatedAtUtc = now
            },
            new SubscriptionPlan
            {
                Id = SubscriptionPlanCode.Enterprise,
                Name = "Enterprise",
                Tagline = "Çoklu mağaza ajansları ve büyük üreticiler için MCP API & kurumsal kotalar",
                BadgeText = "AJANS / KURUMSAL",
                AccentColor = "#f59e0b",
                MonthlyPriceUsd = 249m,
                YearlyPriceUsd = 2390m,
                MonthlyPriceTry = 8650m,
                YearlyPriceTry = 83000m,
                MaxShops = 25,
                MonthlyAiTokenQuota = 10_000_000,
                MaxMonthlyAiListings = -1,
                MaxBatchQueueSize = 250,
                AllowedFeaturesJson = JsonSerializer.Serialize(EnterpriseFeatures),
                IsPopular = false,
                IsActive = true,
                DisplayOrder = 4,
                UpdatedAtUtc = now
            }
        ];
    }
}
