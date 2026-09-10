#pragma warning disable CS0618

namespace SimilarProductsWinForms.Services;

using System;
using System.IO;
using System.Globalization;
using SkiaSharp;
using SimilarProductsWinForms.Models;

/// <summary>
/// Etsy siparişleri için resmi sevk irsaliyesi, konşimento ve fatura detayını (A4 PDF) üreten vektörel servis.
/// </summary>
internal static class EtsyInvoicePdfService
{
    public static string GenerateInvoicePdf(OrderFinancialSummary order, string shopName = "Etsy Store Engine", decimal? customRate = null)
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SimilarProductsWinForms", "Invoices");
        Directory.CreateDirectory(appData);

        var filePath = Path.Combine(appData, $"Invoice_Receipt_{order.ReceiptId}.pdf");

        // A4 Boyutu: 595 x 842 nokta (Points)
        const float pageWidth = 595f;
        const float pageHeight = 842f;
        const float margin = 36f;

        using var outputStream = File.Open(filePath, FileMode.Create, FileAccess.Write);
        using var doc = SKDocument.CreatePdf(outputStream);
        using var canvas = doc.BeginPage(pageWidth, pageHeight);

        decimal rate = customRate ?? (order.ExchangeRate > 0 ? order.ExchangeRate : 48.25m);
        decimal grandTotalTRY = Math.Round(order.GrandTotal * rate, 2);

        // ── 1. Arka Plan & Çerçeve ───────────────────────────────────────────
        using var bgPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
        canvas.DrawRect(0, 0, pageWidth, pageHeight, bgPaint);

        // ── 2. Üst Banner (Modern Gradient/Indigo Başlık) ────────────────────
        using var bannerPaint = new SKPaint
        {
            Color = new SKColor(30, 41, 59), // Dark Slate #1E293B
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(0, 0, pageWidth, 90, bannerPaint);

        using var accentBarPaint = new SKPaint
        {
            Color = new SKColor(99, 102, 241), // Indigo #6366F1
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(0, 86, pageWidth, 4, accentBarPaint);

        // Başlık Yazıları
        using var titlePaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 17,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };
        canvas.DrawText("ETSY SİPARİŞ FATURASI & SEVK İRSALİYESİ", margin, 38, titlePaint);

        using var subTitlePaint = new SKPaint
        {
            Color = new SKColor(203, 213, 225), // Slate-300
            TextSize = 10,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };
        canvas.DrawText("OFFICIAL ORDER INVOICE & COMMERCIAL PACKING SLIP", margin, 56, subTitlePaint);
        canvas.DrawText($"Mağaza: {shopName} | Düzenleme: {DateTime.Now:dd.MM.yyyy HH:mm}", margin, 72, subTitlePaint);

        // Sağ Üst Sipariş No Kutusu
        using var orderBoxBg = new SKPaint
        {
            Color = new SKColor(51, 65, 85), // Slate-700
            Style = SKPaintStyle.Fill
        };
        var orderBoxRect = new SKRect(pageWidth - margin - 170, 18, pageWidth - margin, 72);
        canvas.DrawRoundRect(orderBoxRect, 6, 6, orderBoxBg);

        using var orderBoxTitlePaint = new SKPaint
        {
            Color = new SKColor(148, 163, 184),
            TextSize = 9,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };
        canvas.DrawText("SİPARİŞ NUMARASI", orderBoxRect.Left + 10, orderBoxRect.Top + 18, orderBoxTitlePaint);

        using var orderBoxNumPaint = new SKPaint
        {
            Color = new SKColor(52, 211, 153), // Emerald #34D399
            TextSize = 14,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };
        canvas.DrawText($"#{order.ReceiptId}", orderBoxRect.Left + 10, orderBoxRect.Top + 40, orderBoxNumPaint);

        // ── 3. Bilgi Kartları (Müşteri & Sipariş Künyesi) ─────────────────────
        float infoY = 110;
        float cardWidth = (pageWidth - (margin * 2) - 16) / 2;
        float cardHeight = 105;

        // Sol Kart: Alıcı / Müşteri Bilgileri
        DrawInfoCard(canvas, margin, infoY, cardWidth, cardHeight, "ALICI / MÜŞTERİ BİLGİLERİ", new[]
        {
            ("Alıcı Adı:", !string.IsNullOrWhiteSpace(order.BuyerName) ? order.BuyerName : "Etsy Müşterisi"),
            ("Etsy Kullanıcı ID:", order.BuyerUserId > 0 ? $"#{order.BuyerUserId}" : "—"),
            ("E-Posta:", !string.IsNullOrWhiteSpace(order.BuyerEmail) ? order.BuyerEmail : "Belirtilmemiş"),
            ("Sipariş Tarihi:", order.OrderDate.LocalDateTime.ToString("dd.MM.yyyy HH:mm"))
        });

        // Sağ Kart: Sipariş & Finans Künyesi
        DrawInfoCard(canvas, margin + cardWidth + 16, infoY, cardWidth, cardHeight, "SİPARİŞ & FİNANS KÜNYESİ", new[]
        {
            ("Sipariş Durumu:", order.DisplayStatus),
            ("Sipariş Kuru:", $"1 USD = ₺{rate:N2}"),
            ("Ödeme Yöntemi:", "Etsy Payments (Garantili Çevrimiçi Ödeme)"),
            ("Gönderim Tipi:", "Uluslararası / Standart Ekspres Kargo")
        });

        // ── 4. Ürün Kalemleri Tablosu ─────────────────────────────────────────
        float tableY = 235;
        float tableWidth = pageWidth - (margin * 2);

        // Tablo Başlık Arka Planı
        using var tableHeaderBg = new SKPaint
        {
            Color = new SKColor(241, 245, 249), // Slate-100
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRoundRect(new SKRect(margin, tableY, margin + tableWidth, tableY + 26), 4, 4, tableHeaderBg);

        using var tableHeadTextPaint = new SKPaint
        {
            Color = new SKColor(71, 85, 105), // Slate-600
            TextSize = 9.5f,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };

        canvas.DrawText("#", margin + 8, tableY + 17, tableHeadTextPaint);
        canvas.DrawText("ÜRÜN / İLAN AÇIKLAMASI", margin + 30, tableY + 17, tableHeadTextPaint);
        canvas.DrawText("LİSTİNG NO", margin + 300, tableY + 17, tableHeadTextPaint);
        canvas.DrawText("ADET", margin + 380, tableY + 17, tableHeadTextPaint);
        canvas.DrawText("BİRİM FİYAT", margin + 430, tableY + 17, tableHeadTextPaint);
        canvas.DrawText("TUTAR ($)", margin + 485, tableY + 17, tableHeadTextPaint);

        // Ürün Satırı
        float itemRowY = tableY + 34;
        using var rowBorderPaint = new SKPaint
        {
            Color = new SKColor(226, 232, 240),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        canvas.DrawLine(margin, itemRowY + 35, margin + tableWidth, itemRowY + 35, rowBorderPaint);

        using var itemTextPaint = new SKPaint
        {
            Color = new SKColor(15, 23, 42),
            TextSize = 9.5f,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };

        using var itemBoldPaint = new SKPaint
        {
            Color = new SKColor(15, 23, 42),
            TextSize = 9.5f,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };

        canvas.DrawText("1", margin + 8, itemRowY + 16, itemTextPaint);

        // Uzun başlığı kısaltarak yaz
        string productTitle = order.ProductTitle;
        if (productTitle.Length > 48) productTitle = productTitle.Substring(0, 45) + "...";
        canvas.DrawText(productTitle, margin + 30, itemRowY + 16, itemBoldPaint);

        canvas.DrawText($"#{order.ListingId}", margin + 300, itemRowY + 16, itemTextPaint);
        canvas.DrawText($"{order.Quantity}", margin + 390, itemRowY + 16, itemTextPaint);

        decimal unitPrice = order.Quantity > 0 ? (order.GrandTotal / order.Quantity) : order.GrandTotal;
        canvas.DrawText($"${unitPrice:N2}", margin + 430, itemRowY + 16, itemTextPaint);
        canvas.DrawText($"${order.GrandTotal:N2}", margin + 485, itemRowY + 16, itemBoldPaint);

        // ── 5. Finansal Özet ve KDV / Vergi Tablosu (Sağ Alt) ─────────────────
        float summaryY = itemRowY + 55;
        float summaryWidth = 260;
        float summaryX = pageWidth - margin - summaryWidth;

        using var summaryBoxBg = new SKPaint
        {
            Color = new SKColor(248, 250, 252),
            Style = SKPaintStyle.Fill
        };
        var summaryRect = new SKRect(summaryX, summaryY, summaryX + summaryWidth, summaryY + 165);
        canvas.DrawRoundRect(summaryRect, 6, 6, summaryBoxBg);
        canvas.DrawRoundRect(summaryRect, 6, 6, rowBorderPaint);

        float curSumY = summaryY + 22;
        DrawSummaryRow(canvas, summaryX + 12, summaryWidth - 24, ref curSumY, "Ürün Ara Toplam (Subtotal):", $"${order.Subtotal:N2}");
        DrawSummaryRow(canvas, summaryX + 12, summaryWidth - 24, ref curSumY, "Kargo Bedeli (Shipping):", $"${order.ShippingPrice:N2}");
        if (order.DiscountAmt > 0)
            DrawSummaryRow(canvas, summaryX + 12, summaryWidth - 24, ref curSumY, "Uygulanan İndirim:", $"-${order.DiscountAmt:N2}", isDanger: true);
        if (order.TaxPaidByBuyer > 0)
            DrawSummaryRow(canvas, summaryX + 12, summaryWidth - 24, ref curSumY, "Satış Vergisi (Tax):", $"${order.TaxPaidByBuyer:N2}");

        // Toplam Çizgisi
        canvas.DrawLine(summaryX + 10, curSumY, summaryX + summaryWidth - 10, curSumY, rowBorderPaint);
        curSumY += 18;

        // GRAND TOTAL
        using var grandLabelPaint = new SKPaint
        {
            Color = new SKColor(15, 23, 42),
            TextSize = 11,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };
        using var grandValPaint = new SKPaint
        {
            Color = new SKColor(16, 185, 129), // Emerald
            TextSize = 13,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };

        canvas.DrawText("GENEL TOPLAM ($):", summaryX + 12, curSumY, grandLabelPaint);
        string grandStr = $"${order.GrandTotal:N2}";
        float grandW = grandValPaint.MeasureText(grandStr);
        canvas.DrawText(grandStr, summaryX + summaryWidth - 12 - grandW, curSumY, grandValPaint);

        curSumY += 20;
        using var tryValPaint = new SKPaint
        {
            Color = new SKColor(99, 102, 241), // Indigo
            TextSize = 10.5f,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };
        canvas.DrawText("TL Karşılığı (Sipariş Günü):", summaryX + 12, curSumY, grandLabelPaint);
        string tryStr = $"₺{grandTotalTRY:N2}";
        float tryW = tryValPaint.MeasureText(tryStr);
        canvas.DrawText(tryStr, summaryX + summaryWidth - 12 - tryW, curSumY, tryValPaint);

        // ── 6. Sol Alt: Üretim & Kâr Mutabakatı (İç Muhasebe Notu) ─────────────
        float internalY = summaryY;
        float internalWidth = (pageWidth - (margin * 2)) - summaryWidth - 16;
        var internalRect = new SKRect(margin, internalY, margin + internalWidth, internalY + 165);
        
        using var internalBoxBg = new SKPaint
        {
            Color = new SKColor(254, 252, 232), // Light yellow #FEFCE8
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRoundRect(internalRect, 6, 6, internalBoxBg);
        
        using var internalBorder = new SKPaint
        {
            Color = new SKColor(254, 240, 138),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        canvas.DrawRoundRect(internalRect, 6, 6, internalBorder);

        float curIntY = internalY + 22;
        using var intTitlePaint = new SKPaint
        {
            Color = new SKColor(133, 77, 14),
            TextSize = 10,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };
        canvas.DrawText("İÇ MUHASEBE & NET KÂR DÖKÜMÜ", margin + 12, curIntY, intTitlePaint);
        curIntY += 20;

        DrawSummaryRow(canvas, margin + 12, internalWidth - 24, ref curIntY, "Etsy Komisyonları:", $"${order.EtsyFees:N2}");
        DrawSummaryRow(canvas, margin + 12, internalWidth - 24, ref curIntY, "Ürün Üretim & Kargo Maliyeti:", $"${order.ProductCost:N2}");
        if (order.OffsiteAdFee > 0)
            DrawSummaryRow(canvas, margin + 12, internalWidth - 24, ref curIntY, "Dış Reklam (Offsite Ads):", $"${order.OffsiteAdFee:N2}");

        curIntY += 10;
        using var netProfitPaint = new SKPaint
        {
            Color = order.NetProfitUSD >= 0 ? new SKColor(16, 185, 129) : new SKColor(239, 68, 68),
            TextSize = 11,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };
        canvas.DrawText("BU SİPARİŞTEN NET KÂR:", margin + 12, curIntY, intTitlePaint);
        string netStr = $"${order.NetProfitUSD:N2} (₺{order.NetProfitTRY:N2})";
        float netW = netProfitPaint.MeasureText(netStr);
        canvas.DrawText(netStr, margin + internalWidth - 12 - netW, curIntY, netProfitPaint);

        // ── 7. Alt Bilgi (Footer & Barkod Alanı) ──────────────────────────────
        float footerY = pageHeight - 70;
        canvas.DrawLine(margin, footerY, pageWidth - margin, footerY, rowBorderPaint);

        using var footerPaint = new SKPaint
        {
            Color = new SKColor(148, 163, 184),
            TextSize = 8.5f,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };

        canvas.DrawText("Bu belge, Etsy MarketPlace entegrasyonu tarafından otomatik oluşturulmuş ticari sevk irsaliyesi ve sipariş faturasıdır.", margin, footerY + 18, footerPaint);
        canvas.DrawText("Gümrük ve paketleme amacıyla sevk irsaliyesi (Packing Slip) olarak doğrudan koli içerisine yerleştirilebilir.", margin, footerY + 32, footerPaint);

        // Barkod / Doğrulama Metni (Sağ Alt)
        using var barcodePaint = new SKPaint
        {
            Color = new SKColor(71, 85, 105),
            TextSize = 9.5f,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold)
        };
        string barcodeText = $"*ETSY-REC-{order.ReceiptId}*";
        float bcW = barcodePaint.MeasureText(barcodeText);
        canvas.DrawText(barcodeText, pageWidth - margin - bcW, footerY + 24, barcodePaint);

        doc.EndPage();
        doc.Close();

        return filePath;
    }

    private static void DrawInfoCard(SKCanvas canvas, float x, float y, float width, float height, string title, (string label, string val)[] items)
    {
        using var cardBg = new SKPaint
        {
            Color = new SKColor(248, 250, 252), // Slate-50
            Style = SKPaintStyle.Fill
        };
        using var cardBorder = new SKPaint
        {
            Color = new SKColor(226, 232, 240),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };

        var rect = new SKRect(x, y, x + width, y + height);
        canvas.DrawRoundRect(rect, 6, 6, cardBg);
        canvas.DrawRoundRect(rect, 6, 6, cardBorder);

        using var titlePaint = new SKPaint
        {
            Color = new SKColor(71, 85, 105),
            TextSize = 9.5f,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };
        canvas.DrawText(title, x + 10, y + 18, titlePaint);

        using var linePaint = new SKPaint
        {
            Color = new SKColor(226, 232, 240),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        canvas.DrawLine(x + 10, y + 25, x + width - 10, y + 25, linePaint);

        float itemY = y + 42;
        using var labelPaint = new SKPaint
        {
            Color = new SKColor(100, 116, 139),
            TextSize = 9,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };
        using var valPaint = new SKPaint
        {
            Color = new SKColor(15, 23, 42),
            TextSize = 9,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };

        foreach (var (lbl, val) in items)
        {
            canvas.DrawText(lbl, x + 10, itemY, labelPaint);
            string safeVal = val.Length > 28 ? val.Substring(0, 25) + "..." : val;
            canvas.DrawText(safeVal, x + 95, itemY, valPaint);
            itemY += 16;
        }
    }

    private static void DrawSummaryRow(SKCanvas canvas, float x, float width, ref float y, string label, string val, bool isDanger = false)
    {
        using var labelPaint = new SKPaint
        {
            Color = new SKColor(100, 116, 139),
            TextSize = 9,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };
        using var valPaint = new SKPaint
        {
            Color = isDanger ? new SKColor(239, 68, 68) : new SKColor(15, 23, 42),
            TextSize = 9,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };

        canvas.DrawText(label, x, y, labelPaint);
        float vW = valPaint.MeasureText(val);
        canvas.DrawText(val, x + width - vW, y, valPaint);
        y += 16;
    }
}
