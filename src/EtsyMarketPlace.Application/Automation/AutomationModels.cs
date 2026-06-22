namespace EtsyMarketPlace.Application.Automation;

using EtsyMarketPlace.Application.ShopPerformance;

public sealed class AutomationSettings
{
    public bool Enabled { get; set; }
    public int IntervalHours { get; set; } = 24;
    public int LookbackDays { get; set; } = 7;
    public string OutputDirectory { get; set; } = "";
    public decimal RevenueDropAlertPercent { get; set; } = 20m;
    public decimal OrderDropAlertPercent { get; set; } = 20m;
    public DateTimeOffset? LastRunAt { get; set; }
}

public sealed record AutomationAlert(string Level, string Title, string Detail);

public sealed record AutomationExportResult(string HtmlPath, string CsvPath);

public sealed record AutomationRunResult(
    DateTimeOffset CompletedAt,
    ShopPerformanceComparison Comparison,
    IReadOnlyList<AutomationAlert> Alerts,
    AutomationExportResult Export);

public interface IAutomationSettingsStore
{
    AutomationSettings Load();
    void Save(AutomationSettings settings);
}

public interface IAutomationReportExporter
{
    Task<AutomationExportResult> ExportAsync(
        ShopPerformanceComparison comparison,
        IReadOnlyList<AutomationAlert> alerts,
        string outputDirectory,
        CancellationToken cancellationToken = default);
}
