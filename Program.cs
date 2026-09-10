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
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var msg = e.ExceptionObject?.ToString() ?? "Bilinmeyen kritik hata.";
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EtsyMarketPlace");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "crash.log"), msg);
            }
            catch { }
            MessageBox.Show($"Uygulama başlatılırken hata oluştu:\n{msg}", "Kritik Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        Application.ThreadException += (s, e) =>
        {
            var msg = e.Exception?.ToString() ?? "Bilinmeyen arayüz hatası.";
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EtsyMarketPlace");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "crash.log"), msg);
            }
            catch { }
            MessageBox.Show($"Arayüz hatası:\n{msg}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

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

        if (args.Any(argument => string.Equals(argument, "--verify-controls", StringComparison.OrdinalIgnoreCase)))
        {
            _ = new SimilarProductsWinForms.Controls.ModernVScrollBar();
            _ = new SimilarProductsWinForms.Controls.ModernMultilineTextBox();
            _ = new SimilarProductsWinForms.Controls.ModernScrollPanel();
            _ = new SimilarProductsWinForms.Controls.ModernButtonControl();
            using var testForm = new FastListingCreatorForm(null!);
            Console.WriteLine("CONTROLS_VERIFIED_OK");
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
        var abTestRepository = new EtsyMarketPlace.Infrastructure.AbTesting.SqliteAbTestRepository(databasePath);
        abTestRepository.InitializeAsync().GetAwaiter().GetResult();
        var abTestService = new EtsyMarketPlace.Application.AbTesting.AbTestService(abTestRepository);

        var batchQueueRepository = new EtsyMarketPlace.Infrastructure.BatchQueue.SqliteBatchQueueRepository(databasePath);
        batchQueueRepository.InitializeAsync().GetAwaiter().GetResult();
        var batchQueueProcessorService = new EtsyMarketPlace.Application.BatchQueue.BatchQueueProcessorService(batchQueueRepository, aiListingOptimizer);

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
            aiListingOptimizer,
            abTestService,
            batchQueueProcessorService));
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
