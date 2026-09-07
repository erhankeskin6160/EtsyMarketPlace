namespace SimilarProductsWinForms.Services;

using System.Text.Json;
using EtsyMarketPlace.Application.Automation;

internal sealed class AutomationSettingsStore : IAutomationSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AutomationSettings Load()
    {
        if (!File.Exists(SettingsPath)) return CreateDefault();
        var json = File.ReadAllText(SettingsPath);
        var settings = JsonSerializer.Deserialize<AutomationSettings>(json) ?? CreateDefault();
        if (string.IsNullOrWhiteSpace(settings.OutputDirectory))
            settings.OutputDirectory = DefaultReportDirectory;
        return settings;
    }

    public void Save(AutomationSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static AutomationSettings CreateDefault() => new()
    {
        OutputDirectory = DefaultReportDirectory,
    };

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SimilarProductsWinForms",
        "automation-settings.json");

    private static string DefaultReportDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "EtsyMarketPlace",
        "Reports");
}
