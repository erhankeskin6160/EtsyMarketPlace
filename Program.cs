namespace SimilarProductsWinForms;

using EtsyMarketPlace.Application.KeywordResearch;
using EtsyMarketPlace.Application.Dashboard;
using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Application.ShopPerformance;
using EtsyMarketPlace.Application.Automation;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Infrastructure.Tracking;
using EtsyMarketPlace.Infrastructure.ShopPerformance;
using EtsyMarketPlace.Infrastructure.Automation;
using EtsyMarketPlace.Infrastructure.ListingOptimization;
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
    static void Main(string[] args)
    {
        var databasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EtsyMarketPlace",
            "market-tracking.db");
        var automation = CreateAutomationServices(databasePath);

        if (args.Any(argument => string.Equals(
            argument,
            AutomationHeadlessRunner.CommandLineSwitch,
            StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = new AutomationHeadlessRunner(
                automation.RunService,
                automation.SettingsStore).RunAsync().GetAwaiter().GetResult();
            return;
        }

        ApplicationConfiguration.Initialize();
        var keywordGateway = new EtsyKeywordMarketGateway(new EtsyApiClient(), EtsyApiSettingsStore.Load);
        var analyzeKeywordUseCase = new AnalyzeKeywordUseCase(keywordGateway);
        var trackingService = new TrackingService(new SqliteTrackingRepository(databasePath));
        trackingService.InitializeAsync().GetAwaiter().GetResult();
        var optimizationHistoryService = new ListingOptimizationHistoryService(
            new SqliteListingOptimizationHistoryRepository(databasePath));
        optimizationHistoryService.InitializeAsync().GetAwaiter().GetResult();
        var localListingOptimizer = new ListingOptimizationService();
        var aiListingOptimizer = new OpenAiListingOptimizer(
            AiOptimizationSettingsStore.Load,
            localListingOptimizer);
        var dashboardService = new DashboardService(trackingService);
        using var automationScheduler = new AutomationScheduler(automation.RunService, automation.SettingsStore);
        automationScheduler.Start();
        Application.Run(new DashboardForm(
            analyzeKeywordUseCase,
            trackingService,
            dashboardService,
            automation.PerformanceService,
            automation.HistoryService,
            automation.SettingsStore,
            automationScheduler,
            new WindowsTaskSchedulerService(),
            optimizationHistoryService,
            aiListingOptimizer));
    }

    private static AutomationServices CreateAutomationServices(string databasePath)
    {
        var performanceService = new ShopPerformanceService(
            new EtsyOwnShopGateway(new EtsyApiClient(), EtsyApiSettingsStore.Load));
        var historyService = new ShopPerformanceHistoryService(
            new SqliteShopPerformanceHistoryRepository(databasePath));
        historyService.InitializeAsync().GetAwaiter().GetResult();
        var settingsStore = new AutomationSettingsStore();
        var runService = new AutomationRunService(
            performanceService,
            historyService,
            new FileAutomationReportExporter(),
            settingsStore);
        return new AutomationServices(performanceService, historyService, settingsStore, runService);
    }

    private sealed record AutomationServices(
        ShopPerformanceService PerformanceService,
        ShopPerformanceHistoryService HistoryService,
        AutomationSettingsStore SettingsStore,
        AutomationRunService RunService);
}
