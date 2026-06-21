namespace SimilarProductsWinForms;

using EtsyMarketPlace.Application.KeywordResearch;
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
        Application.Run(new MarketResearchForm(analyzeKeywordUseCase));
    }    
}
