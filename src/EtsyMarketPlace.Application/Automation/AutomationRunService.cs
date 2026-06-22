namespace EtsyMarketPlace.Application.Automation;

using EtsyMarketPlace.Application.ShopPerformance;

public sealed class AutomationRunService(
    ShopPerformanceService performanceService,
    ShopPerformanceHistoryService historyService,
    IAutomationReportExporter reportExporter,
    IAutomationSettingsStore settingsStore)
{
    public async Task<AutomationRunResult> RunAsync(
        AutomationSettings settings,
        DateTimeOffset? now = null,
        CancellationToken cancellationToken = default)
    {
        Validate(settings);
        var completedAt = now ?? DateTimeOffset.Now;
        var localDate = completedAt.LocalDateTime.Date;
        var startDate = localDate.AddDays(-(settings.LookbackDays - 1));
        var start = new DateTimeOffset(startDate, TimeZoneInfo.Local.GetUtcOffset(startDate));
        var endDate = localDate.AddDays(1).AddTicks(-1);
        var end = new DateTimeOffset(endDate, TimeZoneInfo.Local.GetUtcOffset(endDate));

        var comparison = await performanceService.GetComparisonAsync(start, end, cancellationToken);
        await historyService.SaveAsync(comparison.Current, completedAt, cancellationToken);
        var alerts = BuildAlerts(comparison, settings);
        var export = await reportExporter.ExportAsync(
            comparison,
            alerts,
            settings.OutputDirectory,
            cancellationToken);

        settings.LastRunAt = completedAt;
        settingsStore.Save(settings);
        return new AutomationRunResult(completedAt, comparison, alerts, export);
    }

    public static bool IsDue(AutomationSettings settings, DateTimeOffset now) =>
        settings.Enabled &&
        (!settings.LastRunAt.HasValue || now - settings.LastRunAt.Value >= TimeSpan.FromHours(settings.IntervalHours));

    public static IReadOnlyList<AutomationAlert> BuildAlerts(
        ShopPerformanceComparison comparison,
        AutomationSettings settings)
    {
        var alerts = new List<AutomationAlert>();
        AddDropAlert(alerts, "Ciro dususu", comparison.Revenue, settings.RevenueDropAlertPercent);
        AddDropAlert(alerts, "Siparis dususu", comparison.Orders, settings.OrderDropAlertPercent);

        if (comparison.Current.OrderCount == 0)
        {
            alerts.Add(new AutomationAlert(
                "Kritik",
                "Secilen donemde siparis yok",
                $"{comparison.Current.PeriodStart:dd.MM.yyyy}-{comparison.Current.PeriodEnd:dd.MM.yyyy} doneminde odenmis siparis bulunamadi."));
        }

        return alerts;
    }

    private static void AddDropAlert(
        ICollection<AutomationAlert> alerts,
        string title,
        PerformanceMetric metric,
        decimal threshold)
    {
        if (!metric.PercentageChange.HasValue || metric.PercentageChange.Value > -threshold) return;
        alerts.Add(new AutomationAlert(
            "Uyari",
            title,
            $"Onceki doneme gore %{Math.Abs(metric.PercentageChange.Value):0.#} azaldi ({metric.Previous:0.##} -> {metric.Current:0.##})."));
    }

    private static void Validate(AutomationSettings settings)
    {
        if (settings.IntervalHours is < 1 or > 720)
            throw new ArgumentOutOfRangeException(nameof(settings.IntervalHours), "Calisma araligi 1-720 saat olmalidir.");
        if (settings.LookbackDays is < 1 or > 365)
            throw new ArgumentOutOfRangeException(nameof(settings.LookbackDays), "Rapor donemi 1-365 gun olmalidir.");
        if (string.IsNullOrWhiteSpace(settings.OutputDirectory))
            throw new InvalidOperationException("Rapor klasoru secilmelidir.");
    }
}
