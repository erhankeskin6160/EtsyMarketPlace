namespace SimilarProductsWinForms.Services;

using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using SimilarProductsWinForms.Models;

/// <summary>
/// Etsy Payment Account Ledger API servisi.
/// GET /v3/application/shops/{shop_id}/payment-account/ledger-entries
/// Scope: billing_r
/// </summary>
internal sealed class FinancialReportService
{
    private const string BaseUrl = "https://openapi.etsy.com/v3/application";
    private static readonly HttpClient Http = new();
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

    /// <summary>
    /// Etsy Ledger kayıtlarını çeker (gerçek API).
    /// </summary>
    public async Task<FinancialReport> GetReportAsync(
        string shopId,
        string apiKey,
        string accessToken,
        DateTimeOffset from,
        DateTimeOffset to,
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
            req.Headers.Add("x-api-key", apiKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
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

        return BuildReport(entries, from, to);
    }

    /// <summary>
    /// Demo/Mock modu — API bağlantısı olmadan gerçekçi test verisi.
    /// </summary>
    public static FinancialReport GenerateMockReport(DateTimeOffset from, DateTimeOffset to)
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
            ("ad_fee",              2m,   18m, false),
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
                        new DateTimeOffset(d.AddHours(rng.Next(8, 20)), TimeSpan.Zero)
                    ));
                }
            }
        }

        return BuildReport(entries, from, to);
    }

    // ─── Private Yardımcı Metotlar ────────────────────────────────────────────

    private static LedgerEntry MapEntry(LedgerEntryDto dto)
    {
        decimal amount = dto.Amount / 100m;
        decimal netAmount = dto.NetAmount / 100m;
        var ts = DateTimeOffset.FromUnixTimeSeconds(dto.CreateTimestamp);
        var type = (dto.LedgerType ?? dto.ReferenceType ?? "other").ToLowerInvariant();

        return new LedgerEntry(dto.EntryId, type, amount, netAmount,
            dto.Currency ?? "USD", dto.Description ?? string.Empty, ts);
    }

    private static FinancialReport BuildReport(
        List<LedgerEntry> entries,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        decimal totalGross = 0, totalRefunds = 0, totalFees = 0,
                totalAd = 0, totalShipping = 0;

        foreach (var e in entries)
        {
            switch (e.Type)
            {
                case "sale":
                    totalGross += e.Amount;
                    break;
                case "refund":
                    totalRefunds += e.Amount;
                    break;
                case "ad_fee":
                case "offsite_ads":
                    totalAd += e.Amount;
                    totalFees += e.Amount;
                    break;
                case "shipping":
                    totalShipping += e.Amount;
                    break;
                case "listing_fee":
                case "transaction_fee":
                case "payment_processing":
                case "regulatory_operating_fee":
                    totalFees += e.Amount;
                    break;
            }
        }

        decimal totalNet = totalGross - totalRefunds - totalFees + totalShipping;

        // Aylık gruplama
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

        string currency = entries.Select(e => e.Currency).FirstOrDefault() ?? "USD";

        return new FinancialReport(entries, monthly,
            totalGross, totalRefunds, totalFees, totalAd, totalShipping,
            totalNet, currency, from, to);
    }
}
