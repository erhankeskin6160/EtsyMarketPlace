namespace SimilarProductsWinForms.Services;

using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using EtsyMarketPlace.Application.ShopPerformance;
using SimilarProductsWinForms.Models;

/// <summary>
/// Etsy Payment Account Ledger & Receipts API servisi.
/// GET /v3/application/shops/{shop_id}/payment-account/ledger-entries (Scope: billing_r)
/// Fallback: /v3/application/shops/{shop_id}/receipts (Scope: transactions_r)
/// </summary>
internal sealed class FinancialReportService
{
    private const string BaseUrl = "https://api.etsy.com/v3/application";
    private static readonly HttpClient Http = new();
    private readonly EtsyApiClient _apiClient = new();
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ─── Etsy API Yanıt Modelleri ────────────────────────────────────────────

    // DTO kullanılmıyor, JsonElement ile parse edilecek.

    // ─── Public API ──────────────────────────────────────────────────────────

    private static readonly SqliteProductCostRepository ProductCostRepo = new();

    /// <summary>
    /// Etsy gerçek mağaza verilerini çeker (Payment Account Ledger or Receipts fallback).
    /// </summary>
    public async Task<FinancialReport> GetReportAsync(
        EtsyApiSettings settings,
        DateTimeOffset from,
        DateTimeOffset to,
        decimal exchangeRate = 36.50m,
        CancellationToken ct = default)
    {
        var source = await _apiClient.GetOwnShopPerformanceSourceAsync(settings, from, to, ct);
        if (source.Shop.ShopId > 0)
        {
            settings.ShopId = source.Shop.ShopId.ToString(CultureInfo.InvariantCulture);
            EtsyApiSettingsStore.Save(settings);
        }

        // Mağaza fişlerini (receipts) her zaman çek — sipariş bazında net kâr için gerekli
        var receipts = source.Receipts;

        try
        {
            return await GetReportFromLedgerAsync(settings, from, to, receipts, exchangeRate, ct);
        }
        catch (Exception ex) when (ex.Message.Contains("403") || ex.Message.Contains("401") || ex.Message.Contains("billing_r"))
        {
            try { File.WriteAllText("etsy_error_log.txt", ex.ToString()); } catch { }
            // Payment Account (billing_r) yetkisi yoksa gerçek sipariş/satış (transactions_r) verilerini raporla
            return await BuildReportFromReceiptsAsync(receipts, from, to, exchangeRate, ct);
        }
    }

    private async Task<FinancialReport> GetReportFromLedgerAsync(
        EtsyApiSettings settings,
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyList<OwnShopReceipt> receipts,
        decimal exchangeRate,
        CancellationToken ct = default)
    {
        var entries = new List<LedgerEntry>();
        var currentFrom = from;
        const int limit = 100;

        while (currentFrom < to)
        {
            var currentTo = currentFrom.AddDays(30);
            if (currentTo > to) currentTo = to;

            int offset = 0;
            while (true)
            {
                var url = $"{BaseUrl}/shops/{settings.ShopId}/payment-account/ledger-entries" +
                          $"?min_created={currentFrom.ToUnixTimeSeconds()}" +
                          $"&max_created={currentTo.ToUnixTimeSeconds()}" +
                          $"&limit={limit}&offset={offset}";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("x-api-key", $"{settings.Keystring.Trim()}:{settings.SharedSecret?.Trim()}");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken?.Trim());
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var resp = await Http.SendAsync(req, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Etsy API Hatası [{(int)resp.StatusCode}]: {body}");

                using var document = JsonDocument.Parse(body);
                if (!document.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                {
                    break;
                }

                foreach (var el in results.EnumerateArray())
                {
                    entries.Add(MapEntry(el));
                }

                if (results.GetArrayLength() < limit) break;
                offset += limit;
            }

            currentFrom = currentTo.AddSeconds(1);
        }

        // Benzersiz kayıtları al (kesişme ihtimaline karşı)
        entries = entries.DistinctBy(e => e.EntryId).ToList();

        return await BuildReportAsync(entries, receipts, from, to, exchangeRate, ct);
    }

    private static async Task<FinancialReport> BuildReportFromReceiptsAsync(
        IReadOnlyList<OwnShopReceipt> receipts,
        DateTimeOffset from,
        DateTimeOffset to,
        decimal exchangeRate,
        CancellationToken ct = default)
    {
        var entries = new List<LedgerEntry>();
        long id = 1;

        foreach (var r in receipts)
        {
            if (r.IsCanceled)
            {
                entries.Add(new LedgerEntry(
                    id++,
                    "refund",
                    r.GrandTotal,
                    -r.GrandTotal,
                    r.CurrencyCode ?? "USD",
                    $"İade / İptal Sipariş #{r.ReceiptId}",
                    r.CreatedAt));
            }
            else if (r.IsPaid)
            {
                decimal estListingFee = 0.20m;
                decimal estTransactionFee = Math.Round(r.GrandTotal * 0.065m, 2);
                decimal estPaymentProcessing = Math.Round(r.GrandTotal * 0.03m + 0.25m, 2);
                decimal offsiteAdFee = r.IsFromOffsiteAds ? Math.Round(r.GrandTotal * 0.15m, 2) : 0m;

                entries.Add(new LedgerEntry(
                    id++,
                    "sale",
                    r.GrandTotal,
                    r.GrandTotal,
                    r.CurrencyCode ?? "USD",
                    $"Sipariş #{r.ReceiptId} ({(r.Transactions.Count > 0 ? r.Transactions[0].Title : "Etsy Ürün")})",
                    r.CreatedAt));

                entries.Add(new LedgerEntry(
                    id++,
                    "transaction_fee",
                    estTransactionFee,
                    -estTransactionFee,
                    r.CurrencyCode ?? "USD",
                    $"İşlem Komisyonu (%6.5) #{r.ReceiptId}",
                    r.CreatedAt));

                entries.Add(new LedgerEntry(
                    id++,
                    "payment_processing",
                    estPaymentProcessing,
                    -estPaymentProcessing,
                    r.CurrencyCode ?? "USD",
                    $"Ödeme İşleme Ücreti #{r.ReceiptId}",
                    r.CreatedAt));

                entries.Add(new LedgerEntry(
                    id++,
                    "listing_fee",
                    estListingFee,
                    -estListingFee,
                    r.CurrencyCode ?? "USD",
                    $"İlan Ücreti #{r.ReceiptId}",
                    r.CreatedAt));

                if (offsiteAdFee > 0)
                {
                    entries.Add(new LedgerEntry(
                        id++,
                        "offsite_ads",
                        offsiteAdFee,
                        -offsiteAdFee,
                        r.CurrencyCode ?? "USD",
                        $"Dış Reklam Ücreti (%15) #{r.ReceiptId}",
                        r.CreatedAt));
                }
            }
        }

        return await BuildReportAsync(entries, receipts, from, to, exchangeRate, ct, true);
    }

    /// <summary>
    /// Demo/Mock modu — API bağlantısı olmadan gerçekçi test verisi.
    /// </summary>
    public static async Task<FinancialReport> GenerateMockReportAsync(DateTimeOffset from, DateTimeOffset to, decimal exchangeRate = 36.50m)
    {
        var rng = new Random(42);
        var entries = new List<LedgerEntry>();
        long id = 1000;

        var types = new (string type, decimal baseMin, decimal baseMax, bool isCredit)[]
        {
            ("sale",               25m,  180m, true),
            ("refund",             10m,   60m, false),
            ("listing_fee",         0.20m,  0.20m, false),
            ("transaction_fee",     1.50m,  9m,  false),
            ("ad_fee",              2m,   14m, false),
            ("offsite_ads",         4m,   22m, false),
            ("deposit",            50m,  300m, false),
            ("payment_processing",  1m,    6m,  false),
            ("shipping",            3m,   15m, true),
        };

        // Her gün için kayıtlar üret
        for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
        {
            int salesToday = rng.Next(1, 5);
            for (int i = 0; i < salesToday; i++)
            {
                foreach (var (type, min, max, _) in types)
                {
                    if (type != "sale" && rng.NextDouble() > 0.7) continue;
                    decimal amt = Math.Round((decimal)(rng.NextDouble() * (double)(max - min)) + min, 2);
                    decimal net = type == "sale" ? amt * 0.94m : -amt;
                    if (type is "shipping" or "sale") net = Math.Abs(net);
                    if (type != "sale" && type != "shipping") net = -Math.Abs(net);

                    entries.Add(new LedgerEntry(
                        id++, type, amt,
                        Math.Round(net, 2),
                        "USD",
                        $"{type} #{id}",
                        new DateTimeOffset(DateTime.SpecifyKind(d.AddHours(rng.Next(8, 20)), DateTimeKind.Utc))
                    ));
                }
            }
        }

        return await BuildReportInternalAsync(entries, [], from, to, [], exchangeRate, CancellationToken.None, false);
    }

    // ─── Private Yardımcı Metotlar ────────────────────────────────────────────

    private static LedgerEntry MapEntry(JsonElement el)
    {
        long entryId = el.TryGetProperty("entry_id", out var propEntryId) && propEntryId.ValueKind == JsonValueKind.Number ? propEntryId.GetInt64() : 0;
        string rawType = (el.TryGetProperty("ledger_type", out var propType) && propType.ValueKind == JsonValueKind.String ? propType.GetString() : "")?.ToLowerInvariant() ?? "";
        string rawRefType = (el.TryGetProperty("reference_type", out var propRef) && propRef.ValueKind == JsonValueKind.String ? propRef.GetString() : "")?.ToLowerInvariant() ?? "";
        string desc = (el.TryGetProperty("description", out var propDesc) && propDesc.ValueKind == JsonValueKind.String ? propDesc.GetString() : "")?.ToLowerInvariant() ?? "";
        
        long tsSeconds = el.TryGetProperty("create_date", out var propDate) && propDate.ValueKind == JsonValueKind.Number ? propDate.GetInt64() : 0;
        var ts = tsSeconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(tsSeconds) : DateTimeOffset.UtcNow;

        decimal amount = 0m;
        decimal netAmount = 0m;
        string currency = "USD";

        if (el.TryGetProperty("amount", out var amt) && amt.ValueKind == JsonValueKind.Number)
        {
            decimal rawCents = amt.GetDecimal();
            netAmount = rawCents / 100m;
            amount = netAmount;
        }
        
        if (el.TryGetProperty("currency", out var curr) && curr.ValueKind == JsonValueKind.String)
        {
            currency = curr.GetString() ?? "USD";
        }
        
        decimal exRate = HistoricalExchangeRateProvider.GetRateForDate(ts.DateTime, 36.50m);
        
        if (currency.Equals("TRY", StringComparison.OrdinalIgnoreCase))
        {
            amount /= exRate;
            netAmount /= exRate;
            currency = "USD";
        }
        else if (currency.Equals("EUR", StringComparison.OrdinalIgnoreCase))
        {
            amount *= 1.1m;
            netAmount *= 1.1m;
            currency = "USD";
        }
        else if (currency.Equals("GBP", StringComparison.OrdinalIgnoreCase))
        {
            amount *= 1.3m;
            netAmount *= 1.3m;
            currency = "USD";
        }

        string type;

        // 1. Etsy Banka Yatırdığı Tutar (Deposits / Payouts)
        if ((rawType.Contains("deposit") || rawRefType.Contains("deposit") ||
            rawType.Contains("payout") || rawRefType.Contains("payout") ||
            rawType.Contains("disbursement") || rawRefType.Contains("disbursement") ||
            rawType.Contains("transfer") || rawRefType.Contains("transfer") ||
            desc.Contains("deposit") || desc.Contains("payout") || desc.Contains("disbursement") ||
            desc.Contains("transfer") || desc.Contains("yatırılan") || desc.Contains("banka") || desc.Contains("hesaba")) 
            && !rawType.Contains("fee") && !rawRefType.Contains("fee") && !desc.Contains("fee"))
        {
            type = "deposit";
        }
        // 2. Dış Reklam (Offsite Ads)
        else if (rawType.Contains("offsite") || rawRefType.Contains("offsite") || desc.Contains("offsite"))
        {
            type = "offsite_ads";
        }
        // 4. İç Reklam Giderleri (Etsy Ads / Promoted / Search / Marketing / ATS)
        else if (rawType == "ad_fee" || rawType == "ads" || rawRefType == "ad_fee" || rawRefType == "ads" ||
                 rawType.Contains("ats") || rawRefType.Contains("ats") ||
                 rawType.Contains("prolist") || rawRefType.Contains("prolist") ||
                 rawType.Contains("promoted") || rawRefType.Contains("promoted") ||
                 rawType.Contains("marketing") || rawRefType.Contains("marketing") ||
                 desc.Contains("etsy ads") || desc.Contains("reklam") || desc.Contains("promoted") || desc.Contains("marketing"))
        {
            type = "ad_fee";
        }
        // 5. Kargo (Shipping)
        else if (rawType.Contains("shipping") || rawRefType.Contains("shipping") || desc.Contains("shipping"))
        {
            type = "shipping";
        }
        // 6. İlan / Listeleme Ücreti (Listing Fee)
        else if (rawType.Contains("listing") || rawRefType.Contains("listing") || desc.Contains("listing_fee"))
        {
            type = "listing_fee";
        }
        // 7. İşlem Komisyonu (Transaction Fee)
        else if (rawType.Contains("transaction") || rawRefType.Contains("transaction") || desc.Contains("transaction_fee"))
        {
            type = "transaction_fee";
        }
        // 8. Ödeme İşleme Ücreti ve Banka Ücretleri (Processing Fee / Deposit Fee)
        else if (rawType.Contains("processing") || rawRefType.Contains("processing") || desc.Contains("processing_fee") || desc.Contains("deposit fee") || desc.Contains("deposit_fee"))
        {
            type = "payment_processing";
        }
        // 9. KDV / Vergi / Kurumsal Ücretler (VAT / Regulatory / Setup)
        else if (rawType.Contains("vat") || rawRefType.Contains("vat") ||
                 rawType.Contains("regulatory") || rawRefType.Contains("regulatory") ||
                 rawType.Contains("setup") || rawRefType.Contains("setup") ||
                 rawType.Contains("subscription") || rawRefType.Contains("subscription") ||
                 desc.Contains("vat") || desc.Contains("kdv") || desc.Contains("operating fee") || desc.Contains("tax"))
        {
            type = "etsy_tax_fee";
        }
        // 10. İade Tespiti (Refunds / Cancellations) - Komisyon iadeleri yukarıda yakalandığı için buraya sadece ana para iadeleri düşer
        else if (rawType.Contains("refund") || rawRefType.Contains("refund") ||
                 rawType.Contains("cancel") || rawRefType.Contains("cancel") ||
                 desc.Contains("refund") || desc.Contains("iade") || desc.Contains("cancel"))
        {
            type = "refund";
        }
        // 11. Satış Kayıtları (Sales / Orders)
        else if (rawType.Contains("sale") || rawRefType.Contains("sale") ||
                 (rawType.Contains("payment") && !rawType.Contains("processing")) ||
                 desc.Contains("sale") || desc.Contains("sipariş"))
        {
            type = "sale";
        }
        else
        {
            type = rawType.Length > 0 ? rawType : (rawRefType.Length > 0 ? rawRefType : "other");
        }

        return new LedgerEntry(entryId, type, amount, netAmount,
            currency, desc, ts,
            exRate);
    }

    private static async Task<FinancialReport> BuildReportAsync(
        List<LedgerEntry> entries,
        IReadOnlyList<OwnShopReceipt> receipts,
        DateTimeOffset from,
        DateTimeOffset to,
        decimal exchangeRate,
        CancellationToken ct,
        bool isFallbackMode = false)
    {
        var productCosts = await ProductCostRepo.GetAllAsync(ct);
        return await BuildReportInternalAsync(entries, receipts, from, to, productCosts, exchangeRate, ct, isFallbackMode);
    }

    private static async Task<FinancialReport> BuildReportInternalAsync(
        List<LedgerEntry> entries,
        IReadOnlyList<OwnShopReceipt> receipts,
        DateTimeOffset from,
        DateTimeOffset to,
        List<ProductCostEntry> productCosts,
        decimal exchangeRate,
        CancellationToken ct,
        bool isFallbackMode = false)
    {
        decimal totalGross = 0, totalRefunds = 0, totalFees = 0,
                totalInnerAds = 0, totalOffsiteAds = 0, totalDeposits = 0,
                totalShipping = 0, totalProductCosts = 0;

        var costLookup = productCosts.ToDictionary(c => c.ListingId, c => c.TotalUnitCost);

        foreach (var e in entries)
        {
            switch (e.Type)
            {
                case "sale":
                    totalGross += e.Amount;
                    decimal matchedCost = 0;
                    foreach (var kvp in costLookup)
                    {
                        if (e.Description.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            matchedCost = kvp.Value;
                            break;
                        }
                    }
                    if (matchedCost == 0 && costLookup.Count > 0)
                    {
                        matchedCost = costLookup.Values.Average();
                    }
                    totalProductCosts += matchedCost;
                    break;
                case "refund":
                    totalRefunds += e.Amount;
                    break;
                case "ad_fee":
                    totalInnerAds += e.Amount;
                    break;
                case "offsite_ads":
                    totalOffsiteAds += e.Amount;
                    break;
                case "deposit":
                    totalDeposits += e.Amount;
                    break;
                case "shipping":
                    totalShipping += e.Amount;
                    break;
                case "listing_fee":
                case "transaction_fee":
                case "payment_processing":
                case "regulatory_operating_fee":
                case "etsy_tax_fee":
                    totalFees += e.Amount;
                    break;
                default:
                    if (e.Amount > 0 && e.NetAmount < 0)
                    {
                        totalFees += e.Amount;
                    }
                    break;
            }
        }

        decimal totalAdFees = totalInnerAds + totalOffsiteAds;
        // Tüm giderler (refunds, fees, ads) eksi işaretli (negatif) olarak geldiği için, net kârı bulmak adına onları satış (gross) ile 'topluyoruz'.
        decimal totalNet = totalGross + totalRefunds + totalFees + totalAdFees + totalShipping;

        // Aylık gruplama (Eski grafik uyumluluğu için)
        var monthly = entries
            .GroupBy(e => (e.CreatedAt.Year, e.CreatedAt.Month))
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g =>
            {
                var sales  = g.Where(e => e.Type == "sale").Sum(e => e.Amount);
                var refund = g.Where(e => e.Type == "refund").Sum(e => e.Amount);
                var lfee   = g.Where(e => e.Type == "listing_fee").Sum(e => e.Amount);
                var tfee   = g.Where(e => e.Type == "transaction_fee").Sum(e => e.Amount);
                var adfee  = g.Where(e => e.Type is "ad_fee" or "offsite_ads").Sum(e => e.Amount);
                var ship   = g.Where(e => e.Type == "shipping").Sum(e => e.Amount);
                var ppfee  = g.Where(e => e.Type == "payment_processing").Sum(e => e.Amount);
                var other  = g.Where(e => e.Type is not ("sale" or "refund" or "listing_fee"
                    or "transaction_fee" or "ad_fee" or "offsite_ads" or "shipping"
                    or "payment_processing")).Sum(e => e.Amount);

                return new MonthlyFinancial(g.Key.Year, g.Key.Month,
                    sales, refund, lfee, tfee, adfee, ship, ppfee, other);
            })
            .ToList();

        // Dönemsel Muhasebe Özetleri (Kronolojik Tarih Sıralı)
        var dailySummaries = BuildPeriodSummaries(entries, costLookup, exchangeRate, e => (e.CreatedAt.ToString("dd.MM.yyyy"), e.CreatedAt.Date));
        var weeklySummaries = BuildPeriodSummaries(entries, costLookup, exchangeRate, e => ($"{e.CreatedAt.Year}-W{CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(e.CreatedAt.DateTime, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday):D2}", e.CreatedAt.Date.AddDays(-(int)e.CreatedAt.DayOfWeek)));
        var monthlySummaries = BuildPeriodSummaries(entries, costLookup, exchangeRate, e => (e.CreatedAt.ToString("MMM yyyy", CultureInfo.GetCultureInfo("tr-TR")), new DateTime(e.CreatedAt.Year, e.CreatedAt.Month, 1)));
        var yearlySummaries = BuildPeriodSummaries(entries, costLookup, exchangeRate, e => (e.CreatedAt.Year.ToString(), new DateTime(e.CreatedAt.Year, 1, 1)));

        // Sipariş bazında net kâr özetleri (mağaza fişleri üzerinden)
        var orderSummaries = await BuildOrderSummariesAsync(receipts, productCosts, exchangeRate, ct);

        // Sipariş maliyetlerini receipt'lerden topla (daha doğru)
        if (orderSummaries.Count > 0)
        {
            totalProductCosts = orderSummaries.Sum(o => o.ProductCost);
        }

        string currency = entries.Select(e => e.Currency).FirstOrDefault() ?? "USD";

        return new FinancialReport(
            entries, monthly,
            totalGross, totalRefunds, totalFees, totalAdFees,
            totalInnerAds, totalOffsiteAds, totalDeposits, totalProductCosts,
            totalShipping, totalNet, exchangeRate,
            dailySummaries, weeklySummaries, monthlySummaries, yearlySummaries,
            orderSummaries,
            currency, from, to, isFallbackMode);
    }

    /// <summary>
    /// Her Etsy siparişi (receipt) için gerçek net kâr hesaplar.
    /// Formül (Türkiye): GrandTotal - Tax - İşlem(%6.5) - Ödeme(%6.5+3TL) - Yasal(%1.5) - KDV(%20) - İlan - [Dış Reklam(%15)] - Ürün Maliyeti
    /// </summary>
    private static async Task<List<OrderFinancialSummary>> BuildOrderSummariesAsync(
        IReadOnlyList<OwnShopReceipt> receipts,
        List<ProductCostEntry> productCosts,
        decimal defaultExchangeRate,
        CancellationToken ct)
    {
        var result = new List<OrderFinancialSummary>();
        var exchangeService = new ExchangeRateService();
        var costMap = productCosts.ToDictionary(c => c.ListingId, c => c);

        foreach (var r in receipts)
        {
            if (!r.IsPaid || r.IsCanceled) continue;

            var firstTx = r.Transactions.Count > 0 ? r.Transactions[0] : null;
            string title = firstTx?.Title ?? $"Sipariş #{r.ReceiptId}";
            long listingId = firstTx?.ListingId ?? 0;
            int totalQty = r.Transactions.Sum(t => t.Quantity);

            // O günün kuru (Ödeme işleme ücretindeki 3 TL'yi dolara çevirmek ve TRY kârı için)
            decimal rate = await exchangeService.GetHistoricalRateAsync(r.CreatedAt.DateTime, ct);
            if (rate <= 0) rate = defaultExchangeRate;

            decimal grandTotal = r.GrandTotal;
            decimal subtotal = r.Subtotal;
            decimal shippingCost = r.ShippingCost;
            decimal discountAmt = r.DiscountAmt;
            decimal tax = r.TotalTaxCost;

            if (r.CurrencyCode.Equals("TRY", StringComparison.OrdinalIgnoreCase))
            {
                grandTotal /= rate;
                subtotal /= rate;
                shippingCost /= rate;
                discountAmt /= rate;
                tax /= rate;
            }

            // Subtotal boş gelirse GrandTotal'dan türet (İşlem komisyonunun %0 hesaplanmasını önler)
            if (subtotal <= 0 && grandTotal > 0)
            {
                subtotal = Math.Max(0, grandTotal - shippingCost - tax + discountAmt);
            }

            // 1. Vergi Düşülmesi (Etsy'nin alıp hemen kestiği müşteri satış vergisi)
            // 2. İşlem Komisyonu (%6.5) - Ürün bedeli + kargo üzerinden
            decimal subtotalAndShipping = Math.Max(grandTotal, subtotal + shippingCost);
            decimal transactionFee = Math.Round(subtotalAndShipping * 0.065m, 2);

            // 3. Ödeme İşleme Komisyonu (TR için %6.5 + 3 TL sabit)
            decimal trPaymentFixedUsd = Math.Round(3m / rate, 2);
            decimal paymentFee = Math.Round(grandTotal * 0.065m, 2) + trPaymentFixedUsd;

            // 4. Yasal İşlem Ücreti (TR için %1.5)
            decimal regulatoryFee = Math.Round(subtotalAndShipping * 0.015m, 2);

            // 5. İlan Yenileme Ücreti (Listing Fee $0.20)
            decimal listingFee = 0.20m;

            // 6. Dış Reklam (Offsite Ads) Kesimi (%15)
            decimal offsiteAdFee = r.IsFromOffsiteAds ? Math.Round(grandTotal * 0.15m, 2) : 0m;

            // 7. KDV (%20) - Etsy tüm komisyon ve ilan ücretlerinden %20 KDV keser
            decimal totalFeesToTax = transactionFee + paymentFee + regulatoryFee + listingFee + offsiteAdFee;
            decimal vatOnFees = Math.Round(totalFeesToTax * 0.20m, 2);

            // Toplam Etsy Kesintileri
            decimal etsyFees = transactionFee + paymentFee + regulatoryFee + listingFee + vatOnFees + tax;

            // Ürün maliyeti — listing_id üzerinden eşleştir
            decimal unitProductionCost = 0m;
            decimal unitShippingCost = 0m;
            decimal unitPackagingCost = 0m;
            string? invoicePath = null;
            bool    hasCost   = false;
            string  listingIdStr = listingId.ToString(CultureInfo.InvariantCulture);

            if (listingId > 0 && costMap.TryGetValue(listingIdStr, out var entry))
            {
                unitProductionCost = entry.UnitCost;
                unitShippingCost = entry.UnitShippingCost;
                unitPackagingCost = entry.UnitPackagingCost;
                invoicePath = entry.InvoiceFilePath;
                hasCost = true;
            }
            decimal totalUnitCost = unitProductionCost + unitShippingCost + unitPackagingCost;
            decimal productCost = Math.Round(totalUnitCost * Math.Max(1, totalQty), 2);

            // Net kâr hesapla: (Müşterinin ödediği toplam) - (Etsy kesintileri) - (Dış reklam) - (Ürün maliyeti)
            decimal netProfitUSD = Math.Round(grandTotal - etsyFees - offsiteAdFee - productCost, 2);

            // Sipariş gününün kuru ile hesaplanan TRY kârı
            decimal netProfitTRY = Math.Round(netProfitUSD * rate, 2);

            result.Add(new OrderFinancialSummary(
                r.ReceiptId,
                r.CreatedAt,
                title,
                listingId,
                totalQty,
                grandTotal,
                subtotal,
                shippingCost,
                discountAmt,
                tax,
                transactionFee,
                paymentFee,
                regulatoryFee,
                listingFee,
                vatOnFees,
                etsyFees,
                offsiteAdFee,
                productCost,
                netProfitUSD,
                rate,
                netProfitTRY,
                hasCost,
                unitProductionCost,
                unitShippingCost,
                unitPackagingCost,
                invoicePath));
        }

        return result.OrderByDescending(o => o.OrderDate).ToList();
    }

    private static List<PeriodFinancialSummary> BuildPeriodSummaries(
        List<LedgerEntry> entries,
        Dictionary<string, decimal> costLookup,
        decimal exchangeRate,
        Func<LedgerEntry, (string label, DateTime sortDate)> keySelector)
    {
        return entries
            .GroupBy(keySelector)
            .OrderBy(g => g.Key.sortDate)
            .Select(g =>
            {
                var gross = g.Where(e => e.Type == "sale").Sum(e => e.Amount);
                var refunds = g.Where(e => e.Type == "refund").Sum(e => e.Amount);
                var innerAds = g.Where(e => e.Type == "ad_fee").Sum(e => e.Amount);
                var offsiteAds = g.Where(e => e.Type == "offsite_ads").Sum(e => e.Amount);
                var deposits = g.Where(e => e.Type == "deposit").Sum(e => e.Amount);
                var fees = g.Where(e => e.Type is "listing_fee" or "transaction_fee" or "payment_processing" or "regulatory_operating_fee" or "etsy_tax_fee").Sum(e => e.Amount);

                decimal productCosts = 0;
                int salesCount = g.Count(e => e.Type == "sale");
                if (salesCount > 0 && costLookup.Count > 0)
                {
                    productCosts = salesCount * costLookup.Values.Average();
                }

                // Gider kalemleri negatif olduğu için net kârı bulmak amacıyla hepsini topluyoruz.
                decimal netRevenue = gross + refunds + fees + innerAds + offsiteAds;
                decimal realProfitUSD = netRevenue - productCosts;
                
                // Siparişin geldiği günkü kur değerlerinin ortalaması ile hassas TL Kâr hesaplama
                decimal avgExchangeRate = g.Any() ? Math.Round(g.Average(e => e.ExchangeRate), 2) : exchangeRate;
                decimal realProfitTRY = Math.Round(realProfitUSD * avgExchangeRate, 2);

                return new PeriodFinancialSummary(
                    g.Key.label, g.Key.sortDate, gross, fees, innerAds, offsiteAds, refunds, deposits, productCosts,
                    netRevenue, realProfitUSD, realProfitTRY, avgExchangeRate);
            })
            .ToList();
    }
}
