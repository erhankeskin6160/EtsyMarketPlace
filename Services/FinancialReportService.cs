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

    private sealed class LedgerEntriesResponse
    {
        [JsonPropertyName("count")] public int Count { get; set; }
        [JsonPropertyName("results")] public List<LedgerEntryDto>? Results { get; set; }
    }

    private sealed class LedgerEntryDto
    {
        [JsonPropertyName("entry_id")]     public long EntryId { get; set; }
        [JsonPropertyName("ledger_type")]  public string? LedgerType { get; set; }
        [JsonPropertyName("reference_type")] public string? ReferenceType { get; set; }
        [JsonPropertyName("amount")]       public int Amount { get; set; }        // cents
        [JsonPropertyName("net_amount")]   public int NetAmount { get; set; }     // cents
        [JsonPropertyName("currency")]     public string? Currency { get; set; }
        [JsonPropertyName("description")]  public string? Description { get; set; }
        [JsonPropertyName("create_timestamp")] public long CreateTimestamp { get; set; }
    }

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

        try
        {
            return await GetReportFromLedgerAsync(settings.ShopId, settings.Keystring, settings.AccessToken, from, to, exchangeRate, ct);
        }
        catch (Exception ex) when (ex.Message.Contains("403") || ex.Message.Contains("401") || ex.Message.Contains("billing_r"))
        {
            // Payment Account (billing_r) yetkisi yoksa gerçek sipariş/satış (transactions_r) verilerini raporla
            return await BuildReportFromReceiptsAsync(source.Receipts, from, to, exchangeRate, ct);
        }
    }

    private async Task<FinancialReport> GetReportFromLedgerAsync(
        string shopId,
        string apiKey,
        string accessToken,
        DateTimeOffset from,
        DateTimeOffset to,
        decimal exchangeRate,
        CancellationToken ct = default)
    {
        var entries = new List<LedgerEntry>();
        int offset = 0;
        const int limit = 100;

        while (true)
        {
            var url = $"{BaseUrl}/shops/{shopId}/payment-account/ledger-entries" +
                      $"?min_created={from.ToUnixTimeSeconds()}" +
                      $"&max_created={to.ToUnixTimeSeconds()}" +
                      $"&limit={limit}&offset={offset}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("x-api-key", apiKey.Trim());
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Trim());
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var resp = await Http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Etsy API Hatası [{(int)resp.StatusCode}]: {body}");

            var page = JsonSerializer.Deserialize<LedgerEntriesResponse>(body, JsonOpts);
            if (page?.Results == null || page.Results.Count == 0) break;

            entries.AddRange(page.Results.Select(MapEntry));

            if (page.Results.Count < limit) break;
            offset += limit;
        }

        return await BuildReportAsync(entries, from, to, exchangeRate, ct);
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
            }
        }

        return await BuildReportAsync(entries, from, to, exchangeRate, ct);
    }
    }

    /// <summary>
    /// Demo/Mock modu — API bağlantısı olmadan gerçekçi test verisi.
    /// </summary>
    public static FinancialReport GenerateMockReport(DateTimeOffset from, DateTimeOffset to, decimal exchangeRate = 36.50m)
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

        return BuildReportInternal(entries, from, to, [], exchangeRate);
    }

    // ─── Private Yardımcı Metotlar ────────────────────────────────────────────

    private static LedgerEntry MapEntry(LedgerEntryDto dto)
    {
        decimal amount = Math.Abs(dto.Amount / 100m);
        decimal netAmount = dto.NetAmount / 100m;
        var ts = DateTimeOffset.FromUnixTimeSeconds(dto.CreateTimestamp);
        var rawType = (dto.LedgerType ?? "").ToLowerInvariant();
        var rawRefType = (dto.ReferenceType ?? "").ToLowerInvariant();
        var desc = (dto.Description ?? "").ToLowerInvariant();

        string type;

        // 1. Etsy Banka Yatırdığı Tutar (Deposits / Payouts)
        if (rawType.Contains("deposit") || rawRefType.Contains("deposit") ||
            rawType.Contains("payout") || rawRefType.Contains("payout") ||
            rawType.Contains("disbursement") || rawRefType.Contains("disbursement") ||
            rawType.Contains("transfer") || rawRefType.Contains("transfer") ||
            desc.Contains("deposit") || desc.Contains("payout") || desc.Contains("disbursement") ||
            desc.Contains("transfer") || desc.Contains("yatırılan") || desc.Contains("banka") || desc.Contains("hesaba"))
        {
            type = "deposit";
        }
        // 2. İade Tespiti (Refunds / Cancellations)
        else if (rawType.Contains("refund") || rawRefType.Contains("refund") ||
                 rawType.Contains("cancel") || rawRefType.Contains("cancel") ||
                 desc.Contains("refund") || desc.Contains("iade") || desc.Contains("cancel"))
        {
            type = "refund";
        }
        // 3. Dış Reklam (Offsite Ads)
        else if (rawType.Contains("offsite") || rawRefType.Contains("offsite") || desc.Contains("offsite"))
        {
            type = "offsite_ads";
        }
        // 4. İç Reklam Giderleri (Etsy Ads / Promoted / Search)
        else if (rawType.Contains("ad") || rawRefType.Contains("ad") ||
                 rawType.Contains("ats") || rawRefType.Contains("ats") ||
                 rawType.Contains("prolist") || rawRefType.Contains("prolist") ||
                 rawType.Contains("promoted") || rawRefType.Contains("promoted") ||
                 rawType.Contains("marketing") || rawRefType.Contains("marketing") ||
                 desc.Contains("ad_fee") || desc.Contains("promoted") || desc.Contains("etsy ads") || desc.Contains("reklam"))
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
        // 8. Ödeme İşleme Ücreti (Processing Fee)
        else if (rawType.Contains("processing") || rawRefType.Contains("processing") || desc.Contains("processing_fee"))
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
        // 10. Satış Kayıtları (Sales / Orders)
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

        return new LedgerEntry(dto.EntryId, type, amount, netAmount,
            dto.Currency ?? "USD", dto.Description ?? string.Empty, ts,
            HistoricalExchangeRateProvider.GetRateForDate(ts.DateTime, 36.50m));
    }

    private static async Task<FinancialReport> BuildReportAsync(
        List<LedgerEntry> entries,
        DateTimeOffset from,
        DateTimeOffset to,
        decimal exchangeRate,
        CancellationToken ct)
    {
        var productCosts = await ProductCostRepo.GetAllAsync(ct);
        return BuildReportInternal(entries, from, to, productCosts, exchangeRate);
    }

    private static FinancialReport BuildReportInternal(
        List<LedgerEntry> entries,
        DateTimeOffset from,
        DateTimeOffset to,
        List<ProductCostEntry> productCosts,
        decimal exchangeRate)
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
                    totalDeposits += Math.Abs(e.Amount > 0 ? e.Amount : e.NetAmount);
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
        decimal totalNet = totalGross - totalRefunds - totalFees - totalAdFees + totalShipping;

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

        // Dönemsel Muhasebe Özetleri
        var dailySummaries = BuildPeriodSummaries(entries, costLookup, exchangeRate, e => e.CreatedAt.ToString("dd.MM.yyyy"));
        var weeklySummaries = BuildPeriodSummaries(entries, costLookup, exchangeRate, e => $"{e.CreatedAt.Year}-W{CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(e.CreatedAt.DateTime, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday):D2}");
        var monthlySummaries = BuildPeriodSummaries(entries, costLookup, exchangeRate, e => e.CreatedAt.ToString("MMM yyyy", CultureInfo.GetCultureInfo("tr-TR")));
        var yearlySummaries = BuildPeriodSummaries(entries, costLookup, exchangeRate, e => e.CreatedAt.Year.ToString());

        string currency = entries.Select(e => e.Currency).FirstOrDefault() ?? "USD";

        return new FinancialReport(
            entries, monthly,
            totalGross, totalRefunds, totalFees, totalAdFees,
            totalInnerAds, totalOffsiteAds, totalDeposits, totalProductCosts,
            totalShipping, totalNet, exchangeRate,
            dailySummaries, weeklySummaries, monthlySummaries, yearlySummaries,
            currency, from, to);
    }

    private static List<PeriodFinancialSummary> BuildPeriodSummaries(
        List<LedgerEntry> entries,
        Dictionary<string, decimal> costLookup,
        decimal exchangeRate,
        Func<LedgerEntry, string> keySelector)
    {
        return entries
            .GroupBy(keySelector)
            .Select(g =>
            {
                var gross = g.Where(e => e.Type == "sale").Sum(e => e.Amount);
                var refunds = g.Where(e => e.Type == "refund").Sum(e => e.Amount);
                var innerAds = g.Where(e => e.Type == "ad_fee").Sum(e => e.Amount);
                var offsiteAds = g.Where(e => e.Type == "offsite_ads").Sum(e => e.Amount);
                var deposits = g.Where(e => e.Type == "deposit").Sum(e => e.Amount);
                var fees = g.Where(e => e.Type is "listing_fee" or "transaction_fee" or "payment_processing" or "regulatory_operating_fee").Sum(e => e.Amount);

                decimal productCosts = 0;
                int salesCount = g.Count(e => e.Type == "sale");
                if (salesCount > 0 && costLookup.Count > 0)
                {
                    productCosts = salesCount * costLookup.Values.Average();
                }

                decimal netRevenue = gross - refunds - fees - innerAds - offsiteAds;
                decimal realProfitUSD = netRevenue - productCosts;
                
                // Siparişin geldiği günkü kur değerlerinin ortalaması ile hassas TL Kâr hesaplama
                decimal avgExchangeRate = g.Any() ? Math.Round(g.Average(e => e.ExchangeRate), 2) : exchangeRate;
                decimal realProfitTRY = Math.Round(g.Sum(e => e.NetAmountTRY) - (productCosts * avgExchangeRate), 2);

                return new PeriodFinancialSummary(
                    g.Key, gross, fees, innerAds, offsiteAds, refunds, deposits, productCosts,
                    netRevenue, realProfitUSD, realProfitTRY, avgExchangeRate);
            })
            .ToList();
    }
}
