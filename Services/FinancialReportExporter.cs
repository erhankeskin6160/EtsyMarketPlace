namespace SimilarProductsWinForms.Services;

using ClosedXML.Excel;
using System.Text;
using SimilarProductsWinForms.Models;

/// <summary>
/// Finansal raporları Excel (.xlsx) ve CSV formatında dışa aktarır.
/// </summary>
internal static class FinancialReportExporter
{
    // ─── Excel Dışa Aktarma ───────────────────────────────────────────────────

    public static void ExportToExcel(FinancialReport report, string filePath)
    {
        using var wb = new XLWorkbook();

        // — Sayfa 1: Özet ————————————————————————————————————————
        var wsSummary = wb.Worksheets.Add("📊 Finansal Özet");
        BuildSummarySheet(wsSummary, report);

        // — Sayfa 2: Aylık Dökümü ————————————————————————————————
        var wsMonthly = wb.Worksheets.Add("📅 Aylık Döküm");
        BuildMonthlySheet(wsMonthly, report);

        // — Sayfa 3: Ham Kayıtlar ————————————————————————————————
        var wsRaw = wb.Worksheets.Add("📋 Defter Kayıtları");
        BuildRawSheet(wsRaw, report);

        wb.SaveAs(filePath);
    }

    private static void BuildSummarySheet(IXLWorksheet ws, FinancialReport r)
    {
        // Başlık
        ws.Cell("A1").Value = "ETsy Finansal Raporu";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 16;
        ws.Cell("A1").Style.Font.FontColor = XLColor.FromHtml("#6366F1");

        ws.Cell("A2").Value = $"Dönem: {r.PeriodStart:dd.MM.yyyy} — {r.PeriodEnd:dd.MM.yyyy}";
        ws.Cell("A2").Style.Font.Italic = true;
        ws.Cell("A2").Style.Font.FontColor = XLColor.FromHtml("#64748B");

        // Özet tablo başlıkları
        ws.Cell("A4").Value = "Metrik";
        ws.Cell("B4").Value = "Tutar";
        ws.Cell("C4").Value = "Oran";
        StyleHeaderRow(ws.Range("A4:C4"));

        var rows = new (string label, decimal value, string? pct)[]
        {
            ("💰 Brüt Satışlar", r.TotalGross, null),
            ("↩️ İadeler / İptal", r.TotalRefunds, $"-{r.RefundRatePct:F1}%"),
            ("📋 Etsy Ücretleri (Toplam)", r.TotalFees, $"-{r.FeeRatePct:F1}%"),
            ("📢 Reklam Giderleri", r.TotalAdFees, $"-{r.AdSpendPct:F1}%"),
            ("🚚 Kargo Gelirleri", r.TotalShippingCredits, null),
            ("✅ Net Gelir", r.TotalNet, null),
        };

        for (int i = 0; i < rows.Length; i++)
        {
            int row = i + 5;
            ws.Cell(row, 1).Value = rows[i].label;
            ws.Cell(row, 2).Value = (double)rows[i].value;
            ws.Cell(row, 2).Style.NumberFormat.Format = $"[${ r.Currency}]#,##0.00";
            if (rows[i].pct != null)
                ws.Cell(row, 3).Value = rows[i].pct;

            // Net gelir satırını vurgula
            if (rows[i].label.Contains("Net"))
            {
                ws.Range(row, 1, row, 3).Style.Font.Bold = true;
                ws.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0FDF4");
            }
        }

        // Ek istatistikler
        ws.Cell("A12").Value = "📦 Toplam Sipariş Sayısı";
        ws.Cell("B12").Value = r.TransactionCount;
        ws.Cell("A13").Value = "📐 Ortalama Sipariş Değeri";
        ws.Cell("B13").Value = (double)r.AverageOrderValue;
        ws.Cell("B13").Style.NumberFormat.Format = $"[${ r.Currency}]#,##0.00";
        ws.Cell("A14").Value = "💱 Para Birimi";
        ws.Cell("B14").Value = r.Currency;

        ws.Columns().AdjustToContents();
        ws.Column(2).Width = 18;
    }

    private static void BuildMonthlySheet(IXLWorksheet ws, FinancialReport r)
    {
        ws.Cell("A1").Value = "Aylık Finansal Döküm";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;

        var headers = new[] { "Ay", "Brüt Satış", "İade", "Liste Ücreti",
            "İşlem Ücreti", "Reklam", "Kargo", "Ödeme İşlem Ücreti", "Diğer", "NET GELİR" };
        for (int col = 0; col < headers.Length; col++)
            ws.Cell(3, col + 1).Value = headers[col];
        StyleHeaderRow(ws.Range(3, 1, 3, headers.Length));

        int row = 4;
        foreach (var m in r.Monthly)
        {
            ws.Cell(row, 1).Value = m.MonthName;
            ws.Cell(row, 2).Value = (double)m.GrossSales;
            ws.Cell(row, 3).Value = (double)m.Refunds;
            ws.Cell(row, 4).Value = (double)m.ListingFees;
            ws.Cell(row, 5).Value = (double)m.TransactionFees;
            ws.Cell(row, 6).Value = (double)m.AdFees;
            ws.Cell(row, 7).Value = (double)m.ShippingCredits;
            ws.Cell(row, 8).Value = (double)m.PaymentProcessingFees;
            ws.Cell(row, 9).Value = (double)m.OtherFees;
            ws.Cell(row, 10).Value = (double)m.NetIncome;

            // Net gelir rengini koşullu ayarla
            var netCell = ws.Cell(row, 10);
            netCell.Style.Font.Bold = true;
            netCell.Style.Font.FontColor = m.NetIncome >= 0
                ? XLColor.FromHtml("#10B981")
                : XLColor.FromHtml("#EF4444");

            // Para formatı (sütun 2-10)
            for (int c = 2; c <= 10; c++)
                ws.Cell(row, c).Style.NumberFormat.Format = $"[${ r.Currency}]#,##0.00";

            row++;
        }

        // Toplam satırı
        ws.Cell(row, 1).Value = "TOPLAM";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 2).Value = (double)r.TotalGross;
        ws.Cell(row, 3).Value = (double)r.TotalRefunds;
        ws.Cell(row, 10).Value = (double)r.TotalNet;
        for (int c = 2; c <= 10; c++)
        {
            ws.Cell(row, c).Style.Font.Bold = true;
            ws.Cell(row, c).Style.NumberFormat.Format = $"[${ r.Currency}]#,##0.00";
            ws.Cell(row, c).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
        }

        ws.Columns().AdjustToContents();
    }

    private static void BuildRawSheet(IXLWorksheet ws, FinancialReport r)
    {
        ws.Cell("A1").Value = "Defter Kayıtları";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;

        var headers = new[] { "Kayıt ID", "Tür", "Brüt Tutar", "Net Tutar", "Para Birimi", "Açıklama", "Tarih" };
        for (int col = 0; col < headers.Length; col++)
            ws.Cell(3, col + 1).Value = headers[col];
        StyleHeaderRow(ws.Range(3, 1, 3, headers.Length));

        int row = 4;
        foreach (var e in r.Entries.OrderByDescending(x => x.CreatedAt))
        {
            ws.Cell(row, 1).Value = e.EntryId;
            ws.Cell(row, 2).Value = FormatType(e.Type);
            ws.Cell(row, 3).Value = (double)e.Amount;
            ws.Cell(row, 3).Style.NumberFormat.Format = $"[${ r.Currency}]#,##0.00";
            ws.Cell(row, 4).Value = (double)e.NetAmount;
            ws.Cell(row, 4).Style.NumberFormat.Format = $"[${ r.Currency}]#,##0.00";
            ws.Cell(row, 5).Value = e.Currency;
            ws.Cell(row, 6).Value = e.Description;
            ws.Cell(row, 7).Value = e.CreatedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm");
            row++;
        }

        ws.Columns().AdjustToContents();
    }

    // ─── CSV Dışa Aktarma ─────────────────────────────────────────────────────

    public static void ExportToCsv(FinancialReport report, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Kayıt ID,Tür,Brüt Tutar,Net Tutar,Para Birimi,Açıklama,Tarih");

        foreach (var e in report.Entries.OrderByDescending(x => x.CreatedAt))
        {
            sb.AppendLine(string.Join(",",
                e.EntryId,
                $"\"{FormatType(e.Type)}\"",
                e.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                e.NetAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                e.Currency,
                $"\"{e.Description.Replace("\"", "\"\"")}\"",
                e.CreatedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm")
            ));
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    // ─── Yardımcılar ─────────────────────────────────────────────────────────

    private static void StyleHeaderRow(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#6366F1");
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static string FormatType(string type) => type switch
    {
        "sale"                   => "💰 Satış",
        "refund"                 => "↩️ İade",
        "listing_fee"            => "📋 Liste Ücreti",
        "transaction_fee"        => "🔄 İşlem Ücreti",
        "ad_fee"                 => "📢 Reklam",
        "offsite_ads"            => "📢 Dış Reklam",
        "shipping"               => "🚚 Kargo",
        "payment_processing"     => "💳 Ödeme İşlem",
        "regulatory_operating_fee" => "📜 Yasal Ücret",
        _                        => type
    };
}
