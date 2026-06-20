namespace SimilarProductsWinForms.Services;

using System.Net;
using System.Text;
using SimilarProductsWinForms.Models;

internal static class WeeklyReportHtmlExporter
{
    public static string BuildHtml(WeeklyReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html lang=\"tr\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\">");
        builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.AppendLine($"<title>{Encode(report.Title)}</title>");
        builder.AppendLine("<style>");
        builder.AppendLine("""
            body { font-family: "Segoe UI", Arial, sans-serif; margin: 40px; color: #172033; background: #f7f9fc; }
            main { max-width: 980px; margin: 0 auto; background: #fff; padding: 34px; border: 1px solid #d9e1ea; }
            h1 { margin: 0 0 8px; font-size: 30px; }
            h2 { margin: 28px 0 12px; font-size: 20px; border-bottom: 1px solid #d9e1ea; padding-bottom: 8px; }
            pre { white-space: pre-wrap; font-family: "Segoe UI", Arial, sans-serif; font-size: 14px; line-height: 1.5; background: #f3f6fa; padding: 14px; border-radius: 6px; }
            .meta { color: #5b6678; margin-bottom: 22px; }
            .grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; margin: 20px 0; }
            .card { background: #eef6f1; border: 1px solid #cae6d6; padding: 14px; border-radius: 6px; }
            .card strong { display: block; font-size: 13px; color: #146c43; margin-bottom: 8px; }
            @media print {
              body { background: #fff; margin: 0; }
              main { border: 0; max-width: none; }
            }
            """);
        builder.AppendLine("</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("<main>");
        builder.AppendLine($"<h1>{Encode(report.Title)}</h1>");
        builder.AppendLine($"<div class=\"meta\">Olusturma zamani: {DateTime.Now:yyyy-MM-dd HH:mm}</div>");
        builder.AppendLine("<div class=\"grid\">");
        builder.AppendLine(Card("Ozet", report.Summary));
        builder.AppendLine(Card("API Durumu", report.ApiStatus));
        builder.AppendLine(Card("Aksiyon", "Bu hafta odaklanilacak urunleri ve durumlari asagida kontrol edin."));
        builder.AppendLine("</div>");
        Section(builder, "En Yuksek Oncelikli Urunler", report.TopProducts);
        Section(builder, "Durum Dagilimi", report.StatusBreakdown);
        Section(builder, "Kategori Dagilimi", report.CategoryBreakdown);
        Section(builder, "Bu Haftanin Aksiyon Plani", report.ActionPlan);
        builder.AppendLine("</main>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    private static void Section(StringBuilder builder, string title, string text)
    {
        builder.AppendLine($"<h2>{Encode(title)}</h2>");
        builder.AppendLine($"<pre>{Encode(text)}</pre>");
    }

    private static string Card(string title, string text) =>
        $"<div class=\"card\"><strong>{Encode(title)}</strong><pre>{Encode(text)}</pre></div>";

    private static string Encode(string text) => WebUtility.HtmlEncode(text);
}
