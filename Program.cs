namespace SimilarProductsWinForms;

using EtsyMarketPlace.Application.KeywordResearch;
using EtsyMarketPlace.Application.Dashboard;
using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Application.ShopPerformance;
using EtsyMarketPlace.Application.Automation;
using EtsyMarketPlace.Infrastructure.Tracking;
using EtsyMarketPlace.Infrastructure.ShopPerformance;
using SimilarProductsWinForms.Infrastructure.KeywordResearch;
using SimilarProductsWinForms.Infrastructure.ShopPerformance;
using SimilarProductsWinForms.Infrastructure.Automation;
using SimilarProductsWinForms.Services;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        var keywordGateway = new EtsyKeywordMarketGateway(new EtsyApiClient(), EtsyApiSettingsStore.Load);
        var analyzeKeywordUseCase = new AnalyzeKeywordUseCase(keywordGateway);
        var databasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EtsyMarketPlace",
            "market-tracking.db");
        var trackingService = new TrackingService(new SqliteTrackingRepository(databasePath));
        trackingService.InitializeAsync().GetAwaiter().GetResult();
        var dashboardService = new DashboardService(trackingService);
        var shopPerformanceService = new ShopPerformanceService(
            new EtsyOwnShopGateway(new EtsyApiClient(), EtsyApiSettingsStore.Load));
        var shopPerformanceHistoryService = new ShopPerformanceHistoryService(
            new SqliteShopPerformanceHistoryRepository(databasePath));
        shopPerformanceHistoryService.InitializeAsync().GetAwaiter().GetResult();
        var automationSettingsStore = new AutomationSettingsStore();
        var automationRunService = new AutomationRunService(
            shopPerformanceService,
            shopPerformanceHistoryService,
            new FileAutomationReportExporter(),
            automationSettingsStore);
        using var automationScheduler = new AutomationScheduler(automationRunService, automationSettingsStore);
        automationScheduler.Start();
        Application.Run(new DashboardForm(
            analyzeKeywordUseCase,
            trackingService,
            dashboardService,
            shopPerformanceService,
            shopPerformanceHistoryService,
            automationSettingsStore,
            automationScheduler));
    }    
}
