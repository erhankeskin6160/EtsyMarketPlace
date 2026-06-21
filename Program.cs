namespace SimilarProductsWinForms;

using EtsyMarketPlace.Application.KeywordResearch;
using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Infrastructure.Tracking;
using SimilarProductsWinForms.Infrastructure.KeywordResearch;
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
        Application.Run(new MarketResearchForm(analyzeKeywordUseCase, trackingService));
    }    
}
