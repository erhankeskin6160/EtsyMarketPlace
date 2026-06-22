namespace SimilarProductsWinForms.Infrastructure.Automation;

using System.Net;
using System.Text;
using EtsyMarketPlace.Application.Automation;
using EtsyMarketPlace.Application.ShopPerformance;

internal sealed class FileAutomationReportExporter : IAutomationReportExporter
{
    public async Task<AutomationExportResult> ExportAsync(
        ShopPerformanceComparison comparison,
        IReadOnlyList<AutomationAlert> alerts,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var safeShopName = string.Concat(comparison.Current.Shop.ShopName.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var stem = $"{safeShopName}-haftalik-{DateTime.Now:yyyy-MM-dd-HHmmss}";
        var htmlPath = Path.Combine(outputDirectory, stem + ".html");
        var csvPath = Path.Combine(outputDirectory, stem + ".csv");
        await File.WriteAllTextAsync(htmlPath, BuildHtml(comparison, alerts), Encoding.UTF8, cancellationToken);
        await File.WriteAllTextAsync(csvPath, BuildCsv(comparison), new UTF8Encoding(true), cancellationToken);
        return new AutomationExportResult(htmlPath, csvPath);
    }

    internal static string BuildHtml(
        ShopPerformanceComparison comparison,
        IReadOnlyList<AutomationAlert> alerts)
    {
        var current = comparison.Current;
        var previous = comparison.Previous;
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html><html lang=\"tr\"><head><meta charset=\"utf-8\">");
        builder.AppendLine($"<title>{Encode(current.Shop.ShopName)} - Haftalik Rapor</title>");
        builder.AppendLine("""
            <style>
            body{font-family:Segoe UI,Arial,sans-serif;margin:32px;color:#172033;background:#f6f8fb}
            main{max-width:1180px;margin:auto;background:#fff;padding:28px;border:1px solid #dbe2ea}
            .meta{color:#5b6678}.kpis{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin:24px 0}
            .kpi{border:1px solid #dbe2ea;padding:14px}.kpi strong{display:block;color:#5b6678;font-size:13px}
            .kpi b{font-size:24px}.alert{padding:12px;margin:8px 0;background:#fff4e5;border-left:4px solid #d97706}
            table{border-collapse:collapse;width:100%;margin-top:18px}th,td{border:1px solid #dbe2ea;padding:8px;text-align:left}
            th{background:#edf3f8}@media print{body{background:#fff;margin:0}main{border:0}}
            </style></head><body><main>
            """);
        builder.AppendLine($"<h1>{Encode(current.Shop.ShopName)} - Magaza Performans Raporu</h1>");
        builder.AppendLine($"<p class=\"meta\">Secilen donem: {current.PeriodStart:dd.MM.yyyy}-{current.PeriodEnd:dd.MM.yyyy} | Onceki donem: {previous.PeriodStart:dd.MM.yyyy}-{previous.PeriodEnd:dd.MM.yyyy}</p>");
        builder.AppendLine("<div class=\"kpis\">");
        Kpi(builder, "Siparis", current.OrderCount.ToString("N0"), comparison.Orders);
        Kpi(builder, "Satilan adet", current.UnitsSold.ToString("N0"), comparison.Units);
        Kpi(builder, "Brut ciro", $"{current.CurrencyCode} {current.GrossRevenue:N2}", comparison.Revenue);
        Kpi(builder, "Ortalama siparis", $"{current.CurrencyCode} {current.AverageOrderValue:N2}", comparison.AverageOrder);
        builder.AppendLine("</div><h2>Uyarilar</h2>");
        if (alerts.Count == 0) builder.AppendLine("<p>Kritik degisim bulunmadi.</p>");
        foreach (var alert in alerts)
            builder.AppendLine($"<div class=\"alert\"><strong>{Encode(alert.Level)} - {Encode(alert.Title)}</strong><br>{Encode(alert.Detail)}</div>");
        builder.AppendLine("<h2>Urun Performansi</h2><table><thead><tr><th>Listing</th><th>Urun</th><th>Adet</th><th>Onceki adet</th><th>Adet fark</th><th>Ciro</th><th>Onceki ciro</th><th>Ciro fark</th></tr></thead><tbody>");
        foreach (var product in comparison.Products)
        {
            builder.AppendLine($"<tr><td>{product.ListingId}</td><td>{Encode(product.Title)}</td><td>{product.CurrentUnitsSold}</td><td>{product.PreviousUnitsSold}</td><td>{product.UnitDifference:+0;-0;0}</td><td>{product.CurrencyCode} {product.CurrentRevenue:N2}</td><td>{product.CurrencyCode} {product.PreviousRevenue:N2}</td><td>{product.CurrencyCode} {product.RevenueDifference:+0.00;-0.00;0.00}</td></tr>");
        }
        builder.AppendLine("</tbody></table></main></body></html>");
        return builder.ToString();
    }

    internal static string BuildCsv(ShopPerformanceComparison comparison)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Listing,Urun,Guncel siparis,Onceki siparis,Guncel adet,Onceki adet,Adet fark,Guncel ciro,Onceki ciro,Ciro fark,Para birimi");
        foreach (var product in comparison.Products)
        {
            builder.AppendLine(string.Join(",",
                product.ListingId,
                Csv(product.Title),
                product.CurrentOrderCount,
                product.PreviousOrderCount,
                product.CurrentUnitsSold,
                product.PreviousUnitsSold,
                product.UnitDifference,
                product.CurrentRevenue.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                product.PreviousRevenue.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                product.RevenueDifference.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                Csv(product.CurrencyCode)));
        }
        return builder.ToString();
    }

    private static void Kpi(StringBuilder builder, string title, string value, PerformanceMetric metric)
    {
        var percent = metric.PercentageChange.HasValue ? $"%{metric.PercentageChange:+0.#;-0.#;0}" : "Yeni";
        builder.AppendLine($"<div class=\"kpi\"><strong>{Encode(title)}</strong><b>{Encode(value)}</b><div>Onceki: {metric.Previous:0.##} | {Encode(percent)}</div></div>");
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
