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
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString() ?? "Bilinmeyen kritik hata.");
                HandleFatalException(ex);
            };
            Application.ThreadException += (s, e) => HandleFatalException(e.Exception);

            var databasePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EtsyMarketPlace",
                "market-tracking.db");

            var dbDir = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrWhiteSpace(dbDir))
            {
                Directory.CreateDirectory(dbDir);
            }

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
                _ = new SimilarProductsWinForms.Controls.ModernHScrollBar();
                _ = new SimilarProductsWinForms.Controls.ModernMultilineTextBox();
                _ = new SimilarProductsWinForms.Controls.ModernScrollPanel();
                _ = new SimilarProductsWinForms.Controls.ModernButtonControl();
                using var testGrid = new DataGridView();
                _ = SimilarProductsWinForms.Controls.ModernGridScrollAdapter.Attach(testGrid);
                using var f1 = new FastListingCreatorForm(null!);
                using var f2 = new ProfitCalculatorForm();
                using var f3 = new CompetitorAndTrendSpyForm(null!);
                using var f4 = new AiListingImageForm(null!);
                using var f5 = new NotificationSettingsForm();
                using var f6 = new ListingHealthScoreForm(null!);
                using var f7 = new ExternalMarketplaceDiscoveryForm(null!);
                using var f8 = new FinancialReportForm();
                using var f9 = new EtsyListingPreviewDialog("Test Title", 29.99m, "Test Desc", ["tag1"], ["mat1"], [], []);
                using var f10 = new AiStudioImagePickerDialog();
                using var f11 = new StudioGalleryViewerDialog();
                using var f12 = new TrackingHistoryForm(null!);
                using var f13 = new SeoScoreForm(null);
                using var f14 = new OpportunityScoreForm(null);
                using var f15 = new ListingDraftForm(null);
                using var f16 = new ListingOptimizationHistoryForm(null!);
                using var f17 = new PhotoChecklistForm(null);
                using var f18 = new WeeklyReportForm([]);
                using var f19 = new ListingOptimizationForm(null!, null!);
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
        catch (Exception ex)
        {
            HandleFatalException(ex);
        }
    }

    private static void HandleFatalException(Exception ex)
    {
        string msg = ex.ToString();
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EtsyMarketPlace");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "crash.log"), msg);
        }
        catch { }

        try
        {
            var desktopLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ETSY_HATA_RAPORU.txt");
            File.WriteAllText(desktopLog, $"[Tarih: {DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{msg}");
        }
        catch { }

        MessageBox.Show(
            $"Uygulama başlatılırken bir hata oluştu:\n\n{ex.Message}\n\nDetaylar crash.log ve masaüstündeki ETSY_HATA_RAPORU.txt dosyasına yazıldı.",
            "EtsyMarketPlace Başlatma Hatası",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error,
            MessageBoxDefaultButton.Button1,
            MessageBoxOptions.DefaultDesktopOnly);
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
