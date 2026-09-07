namespace SimilarProductsWinForms.Services;

using EtsyMarketPlace.Application.Automation;

internal sealed class AutomationHeadlessRunner(
    AutomationRunService runService,
    IAutomationSettingsStore settingsStore)
{
    public const string CommandLineSwitch = "--automation-run";

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = settingsStore.Load();
            if (!settings.Enabled)
            {
                await WriteLogAsync("Atlandi: Otomatik yenileme ayarlarda etkin degil.", cancellationToken);
                return 2;
            }

            var result = await runService.RunAsync(settings, cancellationToken: cancellationToken);
            await WriteLogAsync(
                $"Basarili | Magaza: {result.Comparison.Current.Shop.ShopName} | " +
                $"Siparis: {result.Comparison.Current.OrderCount} | Uyari: {result.Alerts.Count} | " +
                $"HTML: {result.Export.HtmlPath} | CSV: {result.Export.CsvPath}",
                cancellationToken);
            return 0;
        }
        catch (Exception ex)
        {
            try { await WriteLogAsync($"Hata | {ex}", cancellationToken); }
            catch { }
            return 1;
        }
    }

    public static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EtsyMarketPlace",
        "automation-headless.log");

    private static async Task WriteLogAsync(string message, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        await File.AppendAllTextAsync(
            LogPath,
            $"{DateTimeOffset.Now:O} | {message}{Environment.NewLine}",
            cancellationToken);
    }
}
