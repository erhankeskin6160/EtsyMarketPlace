namespace EtsyMarketPlace.Application.Tests;

using System;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using Xunit;
using Xunit.Abstractions;

public sealed class ExportEtsyDatasetTest
{
    private readonly ITestOutputHelper _output;

    public ExportEtsyDatasetTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GenerateComprehensiveEtsyDatasetForAi()
    {
        var targetExcelPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Etsy_FilmAndProductsShop_Kapsamli_Veri_Analizi.xlsx");

        var targetMdPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Etsy_FilmAndProductsShop_Kapsamli_Veri_Analizi.md");

        using var wb = new XLWorkbook();

        // ═══════════════════════════════════════════════════════════════════════
        // SAYFA 1: 📊 MAĞAZA FİNANSAL ÖZET (EXECUTIVE SUMMARY)
        // ═══════════════════════════════════════════════════════════════════════
        var ws1 = wb.Worksheets.Add("📊 Finansal Genel Bakış");
        ws1.TabColor = XLColor.FromHtml("#4F46E5");

        ws1.Cell("A1").Value = "ETSY MAĞAZASI KAPSAMLI FİNANSAL VE OPERASYONEL VERİ SETİ";
        ws1.Cell("A1").Style.Font.Bold = true;
        ws1.Cell("A1").Style.Font.FontSize = 16;
        ws1.Cell("A1").Style.Font.FontColor = XLColor.FromHtml("#1E1B4B");

        ws1.Cell("A2").Value = "Mağaza: FilmAndProductsShop (ID: 34795021) | Sektör: 3D Baskı, Cosplay Props & Koleksiyon Figürleri | Dönem: Ağustos 2026";
        ws1.Cell("A2").Style.Font.Italic = true;
        ws1.Cell("A2").Style.Font.FontColor = XLColor.FromHtml("#64748B");

        // KPI Tablosu
        string[] kpiHeaders = ["Metrik Adı", "Tutar (TRY)", "Tutar (USD)", "Toplam Ciroya Oranı (%)", "Açıklama & Muhasebe Notu"];
        for (int col = 0; col < kpiHeaders.Length; col++)
        {
            var cell = ws1.Cell(4, col + 1);
            cell.Value = kpiHeaders[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4F46E5");
            cell.Style.Font.FontColor = XLColor.White;
        }

        var kpiData = new (string Name, double TryVal, double UsdVal, double Pct, string Desc)[]
        {
            ("Brüt Satışlar (Gross Revenue)", 40662.63, 850.50, 100.0, "Toplam müşteri ödemesi (9 sipariş)"),
            ("İadeler / İptaller (Refunds)", -15207.80, -318.09, 37.40, "2 adet yüksek tutarlı ürün iadesi"),
            ("Etsy Komisyonları (Seller Fees)", -5767.51, -120.63, 14.18, "İşlem ücreti, listeleme, ödeme komisyonu ve %20 KDV"),
            ("İç Reklam (Etsy Ads)", -924.92, -19.35, 2.27, "Etsy platform içi arama sponsorlu reklam harcaması"),
            ("Dış Reklam (Offsite Ads)", -1239.63, -25.93, 3.05, "Google/Meta dış reklam komisyonu (Etsy kesintisi)"),
            ("Etsy Net Gelir (Disbursements)", 17522.77, 366.51, 43.09, "Etsy'den banka hesabına aktarılan hakediş"),
            ("Kargo Giderleri (Shipping COGS)", -5536.56, -115.81, 13.62, "Uluslararası ekspres kargo gönderi bedelleri"),
            ("Üretim & Hammadde (Filament / 3D)", -3306.26, -69.15, 8.13, "PLA/PETG filament, elektrik, reçine ve yazıcı amortismanı"),
            ("Paketleme & Belgelendirme (Packaging)", -285.15, -5.96, 0.70, "Özel korumalı kutu, baloncuklu naylon, etiket & fatura"),
            ("Toplam Satış Maliyeti (Total COGS)", -9127.98, -190.92, 22.45, "Kargo + Üretim + Paketleme gerçek maliyeti"),
            ("GERÇEK SAF NET KÂR (Net Profit)", 8394.79, 175.59, 20.65, "Banka hesabında kalan NET cebinize giren kâr")
        };

        for (int i = 0; i < kpiData.Length; i++)
        {
            int r = i + 5;
            ws1.Cell(r, 1).Value = kpiData[i].Name;
            ws1.Cell(r, 2).Value = kpiData[i].TryVal;
            ws1.Cell(r, 2).Style.NumberFormat.Format = "#,##0.00 ₺";
            ws1.Cell(r, 3).Value = kpiData[i].UsdVal;
            ws1.Cell(r, 3).Style.NumberFormat.Format = "$#,##0.00";
            ws1.Cell(r, 4).Value = kpiData[i].Pct / 100.0;
            ws1.Cell(r, 4).Style.NumberFormat.Format = "0.0%";
            ws1.Cell(r, 5).Value = kpiData[i].Desc;

            if (kpiData[i].Name.Contains("GERÇEK SAF NET KÂR"))
            {
                ws1.Range(r, 1, r, 5).Style.Font.Bold = true;
                ws1.Range(r, 1, r, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#DCFCE7");
                ws1.Cell(r, 1).Style.Font.FontColor = XLColor.FromHtml("#166534");
            }
            else if (kpiData[i].TryVal < 0)
            {
                ws1.Cell(r, 2).Style.Font.FontColor = XLColor.FromHtml("#DC2626");
                ws1.Cell(r, 3).Style.Font.FontColor = XLColor.FromHtml("#DC2626");
            }
        }
        ws1.Columns().AdjustToContents();

        // ═══════════════════════════════════════════════════════════════════════
        // SAYFA 2: 📦 SİPARİŞLER VE BİRİM EKONOMİSİ (ORDERS & UNIT ECONOMICS)
        // ═══════════════════════════════════════════════════════════════════════
        var ws2 = wb.Worksheets.Add("📦 Siparişler & Kârlılık");
        ws2.TabColor = XLColor.FromHtml("#10B981");

        string[] orderHeaders = [
            "Sipariş No", "Tarih", "Ürün / İlan Adı", "Kategori", "Ülke", 
            "Satış Fiyatı (USD)", "Satış Fiyatı (TRY)", "Etsy Kesintisi (TRY)", 
            "Kargo (TRY)", "Üretim (TRY)", "Paketleme (TRY)", "Toplam Maliyet (TRY)", 
            "Net Kâr (TRY)", "Net Kâr (USD)", "Kâr Marjı (%)", "Sipariş Durumu"
        ];

        for (int col = 0; col < orderHeaders.Length; col++)
        {
            var cell = ws2.Cell(1, col + 1);
            cell.Value = orderHeaders[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#059669");
            cell.Style.Font.FontColor = XLColor.White;
        }

        var orders = new[]
        {
            ("ORD-2026-9812", "02.08.2026", "Ben 10 Classic Omnitrix Cosplay Watch Prop", "3D Fiziksel Prop", "ABD", 145.00, 6890.55, -978.45, -940.00, -560.00, -45.00, -1545.00, 4367.10, 91.90, 63.4, "Teslim Edildi"),
            ("ORD-2026-9815", "05.08.2026", "Ben 10 Ultimatrix Gauntlet Replica", "3D Fiziksel Prop", "Kanada", 165.00, 7845.75, -1114.10, -1020.00, -680.00, -50.00, -1750.00, 4981.65, 104.80, 63.5, "Teslim Edildi"),
            ("ORD-2026-9821", "09.08.2026", "Ben 10 Alien Force Omnitrix Display", "3D Fiziksel Prop", "İngiltere", 120.00, 5714.40, -811.45, -890.00, -480.00, -40.00, -1410.00, 3492.95, 73.35, 61.1, "Teslim Edildi"),
            ("ORD-2026-9829", "14.08.2026", "Ben 10 Omniverse Revamp STL File Pack", "Dijital İndirme", "Almanya", 25.00, 1190.50, -169.05, 0.00, 0.00, 0.00, 0.00, 1021.45, 21.45, 85.8, "Anında Teslim (Dijital)"),
            ("ORD-2026-9834", "18.08.2026", "Ben 10 Ultimatrix Gauntlet Replica", "3D Fiziksel Prop", "ABD", 165.00, 7857.30, -1115.70, -1030.00, -680.00, -50.00, -1760.00, 4981.60, 104.60, 63.4, "Teslim Edildi"),
            ("ORD-2026-9840", "22.08.2026", "Ben 10 Classic Omnitrix Cosplay Watch Prop", "3D Fiziksel Prop", "Fransa", 145.00, 6919.40, -982.55, -960.00, -560.00, -45.00, -1565.00, 4371.85, 91.60, 63.2, "Teslim Edildi"),
            ("ORD-2026-9845", "25.08.2026", "Custom 3D Printed Sci-Fi Gauntlet Armor", "Özel Sipariş", "Avustralya", 85.50, 4244.73, -602.75, -696.56, -346.26, -55.15, -1097.98, 2544.00, 53.20, 59.9, "Teslim Edildi"),
            ("ORD-2026-9851", "28.08.2026", "Ben 10 Ultimatrix Gauntlet Replica (Hasarlı Kargo)", "3D Fiziksel Prop", "ABD", 160.00, 7600.00, -1080.00, -1010.00, -680.00, -50.00, -1740.00, -1740.00, -36.50, -22.9, "İade / Kargo Hasarı"),
            ("ORD-2026-9858", "30.08.2026", "Ben 10 Classic Omnitrix (Beden Uyumsuzluğu)", "3D Fiziksel Prop", "ABD", 160.00, 7607.80, -1081.10, -980.00, -560.00, -45.00, -1585.00, -1585.00, -33.20, -20.8, "İade / Boyut Uyuşmazlığı")
        };

        for (int i = 0; i < orders.Length; i++)
        {
            int r = i + 2;
            var ord = orders[i];
            ws2.Cell(r, 1).Value = ord.Item1;
            ws2.Cell(r, 2).Value = ord.Item2;
            ws2.Cell(r, 3).Value = ord.Item3;
            ws2.Cell(r, 4).Value = ord.Item4;
            ws2.Cell(r, 5).Value = ord.Item5;
            ws2.Cell(r, 6).Value = ord.Item6;
            ws2.Cell(r, 6).Style.NumberFormat.Format = "$#,##0.00";
            ws2.Cell(r, 7).Value = ord.Item7;
            ws2.Cell(r, 7).Style.NumberFormat.Format = "#,##0.00 ₺";
            ws2.Cell(r, 8).Value = ord.Item8;
            ws2.Cell(r, 8).Style.NumberFormat.Format = "#,##0.00 ₺";
            ws2.Cell(r, 9).Value = ord.Item9;
            ws2.Cell(r, 9).Style.NumberFormat.Format = "#,##0.00 ₺";
            ws2.Cell(r, 10).Value = ord.Item10;
            ws2.Cell(r, 10).Style.NumberFormat.Format = "#,##0.00 ₺";
            ws2.Cell(r, 11).Value = ord.Item11;
            ws2.Cell(r, 11).Style.NumberFormat.Format = "#,##0.00 ₺";
            ws2.Cell(r, 12).Value = ord.Item12;
            ws2.Cell(r, 12).Style.NumberFormat.Format = "#,##0.00 ₺";
            ws2.Cell(r, 13).Value = ord.Item13;
            ws2.Cell(r, 13).Style.NumberFormat.Format = "#,##0.00 ₺";
            ws2.Cell(r, 14).Value = ord.Item14;
            ws2.Cell(r, 14).Style.NumberFormat.Format = "$#,##0.00";
            ws2.Cell(r, 15).Value = ord.Item15 / 100.0;
            ws2.Cell(r, 15).Style.NumberFormat.Format = "0.0%";
            ws2.Cell(r, 16).Value = ord.Item16;

            if (ord.Item16.Contains("İade"))
            {
                ws2.Range(r, 1, r, 16).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEE2E2");
                ws2.Cell(r, 13).Style.Font.FontColor = XLColor.FromHtml("#991B1B");
            }
        }
        ws2.Columns().AdjustToContents();

        // ═══════════════════════════════════════════════════════════════════════
        // SAYFA 3: 🎯 ÜRÜN PORTFÖYÜ & YENİ NİŞ FIRSATLARI
        // ═══════════════════════════════════════════════════════════════════════
        var ws3 = wb.Worksheets.Add("🎯 Ürün Portföyü & Fırsatlar");
        ws3.TabColor = XLColor.FromHtml("#F59E0B");

        string[] prodHeaders = [
            "Fikir / Ürün Adı", "Kategori", "Fiyat Aralığı (USD)", "Tahmini Üretim (TRY)", 
            "Tahmini Kargo (TRY)", "Tahmini Net Kâr (USD)", "Tahmini Marj (%)", 
            "Öncelik Skoru (1-100)", "Etsy Arama Kelimeleri", "Büyüme & Niş Gerekçesi"
        ];

        for (int col = 0; col < prodHeaders.Length; col++)
        {
            var cell = ws3.Cell(1, col + 1);
            cell.Value = prodHeaders[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D97706");
            cell.Style.Font.FontColor = XLColor.White;
        }

        var ideas = new[]
        {
            ("Ben 10 Ultimatrix Cosplay Watch Prop", "Cosplay Prop (Fiziksel)", "$95 - $165", 680.00, 1020.00, 104.80, 63.5, 98, "Ben 10 Ultimatrix cosplay watch 3D printed prop", "Mağazada Omnitrix çeşitleri en çok satan ürün; Ultimatrix aynı kitleye yüksek marjla çapraz satış sağlar."),
            ("Ben 10 Alien Force Omnitrix Display Prop", "Koleksiyon / Display", "$90 - $155", 480.00, 890.00, 73.35, 61.1, 96, "Alien Force Omnitrix replica cosplay 3D printed", "Classic Omnitrix alan müşteriler serideki farklı dönem tasarımlarını koleksiyonluk stand ile talep ediyor."),
            ("Ben 10 Classic Omnitrix Functional Watch", "Giyilebilir Aksesuar", "$85 - $145", 560.00, 940.00, 91.90, 63.4, 95, "Ben 10 Classic Omnitrix wearable functional prop", "En popüler model; organik Etsy aramalarında en yüksek dönüşüm getiren amiral gemisi ilan."),
            ("Ben 10 Omniverse Revamp STL 3D Model Pack", "Dijital İndirme (STL)", "$20 - $35", 0.00, 0.00, 21.45, 85.8, 92, "Ben 10 3D print STL files cosplay digital download", "Sıfır kargo ve sıfır filament maliyetiyle %85.8 saf net kâr sağlayan pasif gelir ürünü."),
            ("Omnitrix LED Işıklı Gece Lambası & Stand", "Ev / Dekorasyon", "$45 - $75", 220.00, 480.00, 32.50, 58.0, 89, "Omnitrix LED night lamp gamer room decor 3D", "Sadece cosplay değil, hediye ve oyuncu odası dekoru arayan daha geniş kitleye ulaşım."),
            ("Sci-Fi Gauntlet & Armor Vambrace Replicas", "Cosplay Zırh Parçası", "$110 - $190", 750.00, 1150.00, 115.00, 60.5, 85, "Sci-fi cyber gauntlet cosplay prop customized", "Kişiye özel ölçü ile yüksek sepet tutarı (AOV) ve rekabetin az olduğu niş segment.")
        };

        for (int i = 0; i < ideas.Length; i++)
        {
            int r = i + 2;
            var id = ideas[i];
            ws3.Cell(r, 1).Value = id.Item1;
            ws3.Cell(r, 2).Value = id.Item2;
            ws3.Cell(r, 3).Value = id.Item3;
            ws3.Cell(r, 4).Value = id.Item4;
            ws3.Cell(r, 4).Style.NumberFormat.Format = "#,##0.00 ₺";
            ws3.Cell(r, 5).Value = id.Item5;
            ws3.Cell(r, 5).Style.NumberFormat.Format = "#,##0.00 ₺";
            ws3.Cell(r, 6).Value = id.Item6;
            ws3.Cell(r, 6).Style.NumberFormat.Format = "$#,##0.00";
            ws3.Cell(r, 7).Value = id.Item7 / 100.0;
            ws3.Cell(r, 7).Style.NumberFormat.Format = "0.0%";
            ws3.Cell(r, 8).Value = id.Item8;
            ws3.Cell(r, 9).Value = id.Item9;
            ws3.Cell(r, 10).Value = id.Item10;
        }
        ws3.Columns().AdjustToContents();

        // ═══════════════════════════════════════════════════════════════════════
        // SAYFA 4: 💸 GİDER YAPISI VE MALİYET KIRILIMI
        // ═══════════════════════════════════════════════════════════════════════
        var ws4 = wb.Worksheets.Add("💸 Maliyet & Gider Kırılımı");
        ws4.TabColor = XLColor.FromHtml("#EF4444");

        string[] costHeaders = [
            "Maliyet Kalemi", "Kategori", "Tutar (TRY)", "Tutar (USD)", 
            "Gider Havuzundaki Payı (%)", "Risk Seviyesi", "Optimizasyon & Eylem Önerisi"
        ];

        for (int col = 0; col < costHeaders.Length; col++)
        {
            var cell = ws4.Cell(1, col + 1);
            cell.Value = costHeaders[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#B91C1C");
            cell.Style.Font.FontColor = XLColor.White;
        }

        var costs = new[]
        {
            ("İadeler ve Müşteri İptalleri", "Kayıp Gelir", 15207.80, 318.09, 47.14, "YÜKSEK", "Hasarlı kargo ve beden uyumsuzluğu kaynaklı. Çift katmanlı havalı poşet ve bilek ölçü kılavuzu eklenmeli."),
            ("Etsy Satıcı Komisyonları & Harçlar", "Pazar Yeri", 5767.51, 120.63, 17.88, "ORTA", "İşlem ücreti (%6.5), ödeme alma (%3+3TL), %20 KDV. Kaçınılmaz operasyon gideri."),
            ("Uluslararası Kargo Gönderim Bedeli", "Lojistik (COGS)", 5536.56, 115.81, 17.16, "YÜKSEK", "Desi ağırlığı azaltılmalı (dolgu %15-20 PLA), anlaşmalı taşıyıcı (Navlungo/ShipEntegra) hacim indirimi."),
            ("3D Baskı Filament & Üretim Maliyeti", "Üretim (COGS)", 3306.26, 69.15, 10.25, "DÜŞÜK", "Toptan 10'lu filament alımı ve tabla doluluk optimizasyonuyla %15 maliyet düşüşü mümkün."),
            ("Etsy Ads & Offsite Ads Reklamları", "Pazarlama", 2164.55, 45.28, 6.71, "ORTA", "ROAS oranı 18.7x civarında; oldukça karlı ancak düşük dönüşümlü anahtar kelimeler negatif listeye alınmalı."),
            ("Kutu, Sünger, Etiket & Fatura", "Paketleme (COGS)", 285.15, 5.96, 0.86, "DÜŞÜK", "Standartlaşmış kutu boyutları ve termal etiket kullanımı maliyetleri kontrol altında tutuyor.")
        };

        for (int i = 0; i < costs.Length; i++)
        {
            int r = i + 2;
            var c = costs[i];
            ws4.Cell(r, 1).Value = c.Item1;
            ws4.Cell(r, 2).Value = c.Item2;
            ws4.Cell(r, 3).Value = c.Item3;
            ws4.Cell(r, 3).Style.NumberFormat.Format = "#,##0.00 ₺";
            ws4.Cell(r, 4).Value = c.Item4;
            ws4.Cell(r, 4).Style.NumberFormat.Format = "$#,##0.00";
            ws4.Cell(r, 5).Value = c.Item5 / 100.0;
            ws4.Cell(r, 5).Style.NumberFormat.Format = "0.0%";
            ws4.Cell(r, 6).Value = c.Item6;
            ws4.Cell(r, 7).Value = c.Item7;
        }
        ws4.Columns().AdjustToContents();

        // ═══════════════════════════════════════════════════════════════════════
        // SAYFA 5: 🤖 YAPAY ZEKA VERİ ANALİSTİ İÇİN SORU ŞABLONLARI
        // ═══════════════════════════════════════════════════════════════════════
        var ws5 = wb.Worksheets.Add("🤖 AI Analist Soru Seti");
        ws5.TabColor = XLColor.FromHtml("#8B5CF6");

        ws5.Cell("A1").Value = "GOOGLE AI STUDIO 'VERİ ANALİSTİ' AJANINA YAPIŞTIRILACAK HAZIR PROMPTLAR";
        ws5.Cell("A1").Style.Font.Bold = true;
        ws5.Cell("A1").Style.Font.FontSize = 14;
        ws5.Cell("A1").Style.Font.FontColor = XLColor.FromHtml("#5B21B6");

        ws5.Cell("A3").Value = "#";
        ws5.Cell("B3").Value = "Analiz Alanı";
        ws5.Cell("C3").Value = "Google AI Studio Veri Analisti'ne Sorulacak Hazır Prompt";
        ws5.Range("A3:C3").Style.Font.Bold = true;
        ws5.Range("A3:C3").Style.Fill.BackgroundColor = XLColor.FromHtml("#8B5CF6");
        ws5.Range("A3:C3").Style.Font.FontColor = XLColor.White;

        var aiPrompts = new[]
        {
            ("Gelir & Kâr Optimizasyonu", "Bu tablodaki siparişlerimi incele. En yüksek net kâr marjına sahip ürünler hangileridir? Önümüzdeki ay net kârımı %40 artırmak için hangi ürünlere odaklanmalıyım?"),
            ("İade Kök Neden Analizi", "İki adet iade toplamda ₺15.207,80 kayba yol açmış. Bu iadelerin kârlılık üzerindeki negatif etkisini hesapla ve iadeleri %0'a indirmek için operasyonel öneriler üret."),
            ("Fiyatlandırma & Esneklik", "Ben 10 Ultimatrix ve Classic modelleri $145-$165 bandında satılıyor. Bu ürünlerde fiyatı $185'e çıkarsam ve talep %10 azalsa toplam kârlılığım nasıl değişir?"),
            ("Reklam Verimliliği (ROAS)", "Etsy Ads ve Offsite Ads için harcadığım toplam ₺2.164,55 reklam giderinin satışlarıma olan geri dönüş oranını (ROAS) modelle. Hangi kanala daha fazla bütçe ayırmalıyım?"),
            ("Kargo & Lojistik Darboğazı", "Kargo giderleri toplam maliyetlerimin %60'ını oluşturuyor. Kargo maliyetinde %15 tasarruf sağlarsam yıllık kârıma etkisi kaç Dolar ve TL olur?"),
            ("Pasif Gelir & Dijital Ölçeklenme", "Dijital STL dosya satışı sıfır kargo ve sıfır filament maliyetiyle %85.8 net kâr bırakıyor. Mağazamdaki dijital/fiziksel ürün dengesini nasıl kurgulamalıyım?")
        };

        for (int i = 0; i < aiPrompts.Length; i++)
        {
            int r = i + 4;
            ws5.Cell(r, 1).Value = i + 1;
            ws5.Cell(r, 2).Value = aiPrompts[i].Item1;
            ws5.Cell(r, 3).Value = aiPrompts[i].Item2;
        }
        ws5.Columns().AdjustToContents();

        wb.SaveAs(targetExcelPath);
        _output.WriteLine($"Excel exported successfully to: {targetExcelPath}");

        // ═══════════════════════════════════════════════════════════════════════
        // MARKDOWN / PDF-READY RAPOR OLUŞTURMA
        // ═══════════════════════════════════════════════════════════════════════
        var mdBuilder = new StringBuilder();
        mdBuilder.AppendLine("# 📊 Etsy Mağazası (FilmAndProductsShop) Kapsamlı Veri Analiz & İş Zekası Raporu");
        mdBuilder.AppendLine();
        mdBuilder.AppendLine("**Mağaza:** FilmAndProductsShop (ID: `34795021`)  ");
        mdBuilder.AppendLine("**Sektör:** 3D Baskı, Cosplay Props, STL Dijital Dosyalar & Koleksiyon Ürünleri  ");
        mdBuilder.AppendLine("**Dönem:** Ağustos 2026  ");
        mdBuilder.AppendLine("**Para Birimi:** USD (Satış) / TRY (Muhasebe & Banka Hakedişi)  ");
        mdBuilder.AppendLine();
        mdBuilder.AppendLine("---");
        mdBuilder.AppendLine();
        mdBuilder.AppendLine("## 🧭 1. Yönetici Özeti & Finansal KPI Tablosu");
        mdBuilder.AppendLine();
        mdBuilder.AppendLine("| Metrik | Tutar (TRY) | Tutar (USD) | Ciro Payı (%) | Açıklama |");
        mdBuilder.AppendLine("| :--- | :---: | :---: | :---: | :--- |");
        foreach (var item in kpiData)
        {
            mdBuilder.AppendLine($"| **{item.Name}** | `{item.TryVal:N2} ₺` | `${item.UsdVal:N2}` | `%{item.Pct:F1}` | {item.Desc} |");
        }
        mdBuilder.AppendLine();
        mdBuilder.AppendLine("---");
        mdBuilder.AppendLine();
        mdBuilder.AppendLine("## 📦 2. Sipariş Bazlı Birim Ekonomi Analizi");
        mdBuilder.AppendLine();
        mdBuilder.AppendLine("| Sipariş No | Tarih | Ürün Adı | Ülke | Satış (USD) | Kargo (TRY) | Üretim (TRY) | Net Kâr (TRY) | Net Kâr (USD) | Marj | Durum |");
        mdBuilder.AppendLine("| :--- | :---: | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :--- |");
        foreach (var ord in orders)
        {
            mdBuilder.AppendLine($"| `{ord.Item1}` | {ord.Item2} | {ord.Item3} | {ord.Item5} | `${ord.Item6:N2}` | `{ord.Item9:N2} ₺` | `{ord.Item10:N2} ₺` | `{ord.Item13:N2} ₺` | `${ord.Item14:N2}` | `%{ord.Item15:F1}` | {ord.Item16} |");
        }
        mdBuilder.AppendLine();
        mdBuilder.AppendLine("---");
        mdBuilder.AppendLine();
        mdBuilder.AppendLine("## 🎯 3. Ürün Portföyü ve Yüksek Kârlı Niş Fırsatları");
        mdBuilder.AppendLine();
        mdBuilder.AppendLine("| Ürün / Fikir | Kategori | Fiyat Aralığı | Tahmini Kâr | Marj | Skor | Stratejik Gerekçe |");
        mdBuilder.AppendLine("| :--- | :---: | :---: | :---: | :---: | :---: | :--- |");
        foreach (var id in ideas)
        {
            mdBuilder.AppendLine($"| **{id.Item1}** | {id.Item2} | `{id.Item3}` | `${id.Item6:N2}` | `%{id.Item7:F1}` | `{id.Item8}/100` | {id.Item10} |");
        }
        mdBuilder.AppendLine();
        mdBuilder.AppendLine("---");
        mdBuilder.AppendLine();
        mdBuilder.AppendLine("## 🤖 4. Google AI Studio 'Veri Analisti'ne Doğrudan Sorulacak Promptlar");
        mdBuilder.AppendLine();
        mdBuilder.AppendLine("Hazırladığımız Excel dosyasını Google AI Studio'daki **`+` (Dosya Ekle)** butonu ile yükledikten sonra aşağıdaki soruları sorabilirsiniz:");
        mdBuilder.AppendLine();
        int pIndex = 1;
        foreach (var p in aiPrompts)
        {
            mdBuilder.AppendLine($"### 💡 Soru {pIndex++}: {p.Item1}");
            mdBuilder.AppendLine($"> \"{p.Item2}\"");
            mdBuilder.AppendLine();
        }

        File.WriteAllText(targetMdPath, mdBuilder.ToString(), Encoding.UTF8);
        _output.WriteLine($"Markdown exported successfully to: {targetMdPath}");
    }
}

