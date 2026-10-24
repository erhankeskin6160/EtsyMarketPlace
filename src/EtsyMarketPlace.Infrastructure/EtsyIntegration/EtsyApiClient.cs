using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EtsyMarketPlace.Application.EtsyIntegration;
using Microsoft.Extensions.Options;

namespace EtsyMarketPlace.Infrastructure.EtsyIntegration;

public sealed class EtsyApiClient(HttpClient httpClient, IOptions<EtsyApiOptions> options, IShopSettingsRepository settingsRepository) : IEtsyDataClient
{
    private readonly EtsyApiOptions _options = options.Value;

    public Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default) =>
        GetPagedAsync("application/shops/{0}/payment-account/ledger-entries", shopId, startDate, endDate, ParsePayout, cancellationToken);

    public Task<IReadOnlyList<EtsyFinancialTransaction>> GetFinancialTransactionsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default) =>
        GetPagedAsync("application/shops/{0}/transactions", shopId, startDate, endDate, ParseTransaction, cancellationToken);

    public async Task<IReadOnlyList<EtsyOrderCostAlert>> GetUnfulfilledCostAlertsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        var receipts = await GetPagedAsync("application/shops/{0}/receipts", shopId, DateTimeOffset.UtcNow.AddDays(-90), DateTimeOffset.UtcNow, ParseOrder, cancellationToken);
        return receipts;
    }

    /// <summary>Web kontrol paneli: verilen aralikta magaza fislerini (siparisleri) Etsy API'den canli ceker.</summary>
    public Task<IReadOnlyList<EtsyDashboardReceipt>> GetShopReceiptsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default) =>
        GetPagedAsync("application/shops/{0}/receipts", shopId, startDate, endDate, ParseDashboardReceipt, cancellationToken);

    /// <summary>Web kontrol paneli: odeme hesabi defter kayitlari (dis reklam kesintilerinin siparis eslesmesi icin).</summary>
    public Task<IReadOnlyList<EtsyDashboardLedgerFee>> GetPaymentAccountLedgerEntriesAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default) =>
        GetPagedAsync("application/shops/{0}/payment-account/ledger-entries", shopId, startDate, endDate, ParseDashboardLedgerFee, cancellationToken);

    /// <summary>
    /// Sunucu finans motoru: tam alanli odeme hesabi defter kayitlari. Tutarlar USD'ye cevrilir ve
    /// masaustu MapEntry kurallariyla siniflandirilir (parite: docs/finans-motoru-ve-parite.md).
    /// </summary>
    public Task<IReadOnlyList<EtsyLedgerEntryDetail>> GetLedgerEntriesDetailedAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default) =>
        GetPagedAsync("application/shops/{0}/payment-account/ledger-entries", shopId, startDate, endDate, ParseLedgerEntryDetail, cancellationToken);

    /// <summary>
    /// Etsy istekleri icin x-api-key degerini cozer: once magaza ayarlari (DPAPI sifreli kayit),
    /// yoksa yapilandirma (appsettings) kullanilir. Token uclariyla ayni kalip (EtsyOAuthService.ResolveCredentialsAsync).
    /// </summary>
    private async Task<string> ResolveApiKeyHeaderAsync(string shopId, CancellationToken cancellationToken)
    {
        var keystring = string.Empty;
        var sharedSecret = string.Empty;
        try
        {
            var raw = await settingsRepository.GetRawEtsyAppCredentialsAsync(shopId, cancellationToken);
            keystring = raw.Keystring;
            sharedSecret = raw.SharedSecret;
        }
        catch
        {
            // Magaza ayarlari okunamazsa yapilandirmaya dusulur.
        }

        if (string.IsNullOrWhiteSpace(keystring)) keystring = _options.ApiKey;
        if (string.IsNullOrWhiteSpace(sharedSecret)) sharedSecret = _options.SharedSecret;

        if (string.IsNullOrWhiteSpace(keystring))
            throw new InvalidOperationException("Etsy API keystring (Client ID) yapilandirilmamis. Lutfen Ayarlar > Etsy API sayfasindan kaydedin.");

        return !string.IsNullOrWhiteSpace(sharedSecret) ? $"{keystring.Trim()}:{sharedSecret.Trim()}" : keystring.Trim();
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTimeOffset ExpiresAt, Task<IReadOnlyList<JsonElement>> Task)> _ledgerWindowCache = new();

    private async Task<IReadOnlyList<T>> GetPagedAsync<T>(string routeTemplate, string shopId, DateTimeOffset startDate, DateTimeOffset endDate, Func<JsonElement, string, T?> parser, CancellationToken cancellationToken)
        where T : class
    {
        if (routeTemplate.Contains("payment-account/ledger-entries", StringComparison.OrdinalIgnoreCase))
        {
            var rawItems = await GetRawLedgerItemsChunkedAsync(routeTemplate, shopId, startDate, endDate, cancellationToken);
            var parsedResults = new List<T>(rawItems.Count);
            foreach (var item in rawItems)
            {
                var parsed = parser(item, shopId);
                if (parsed is not null) parsedResults.Add(parsed);
            }
            return parsedResults;
        }

        return await GetSingleRangePagedAsync(routeTemplate, shopId, startDate, endDate, parser, cancellationToken);
    }

    private async Task<IReadOnlyList<JsonElement>> GetRawLedgerItemsChunkedAsync(
        string routeTemplate,
        string shopId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken)
    {
        // Ana thread uzerinde tek bir kez API anahtarini coz (EF Core DbContext thread-safety korunur)
        var apiKeyHeader = await ResolveApiKeyHeaderAsync(shopId, cancellationToken);

        // Tarihsel kurlari parse oncesinde tek seferde hidrate et
        try
        {
            await HistoricalExchangeRateProvider.HydrateRatesForDatesAsync(
                new[] { startDate.UtcDateTime.Date, endDate.UtcDateTime.Date },
                cancellationToken);
        }
        catch
        {
            // Kur API'si ulasilamazsa varsayilan kurlarla devam edilir
        }

        var windows = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        var currentFrom = startDate;
        while (currentFrom < endDate)
        {
            var currentTo = currentFrom.AddDays(30);
            if (currentTo > endDate) currentTo = endDate;
            windows.Add((currentFrom, currentTo));
            currentFrom = currentTo.AddSeconds(1);
        }

        if (windows.Count == 0)
        {
            windows.Add((startDate, endDate));
        }

        if (windows.Count == 1)
        {
            var (wStart, wEnd) = windows[0];
            return await GetCachedLedgerWindowAsync(routeTemplate, shopId, apiKeyHeader, wStart, wEnd, cancellationToken);
        }

        var windowResults = new IReadOnlyList<JsonElement>[windows.Count];
        using var semaphore = new SemaphoreSlim(4, 4);
        var tasks = windows.Select(async (window, index) =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                windowResults[index] = await GetCachedLedgerWindowAsync(routeTemplate, shopId, apiKeyHeader, window.Start, window.End, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        var combined = new List<JsonElement>();
        var seenEntryIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var batch in windowResults)
        {
            if (batch is null) continue;
            foreach (var item in batch)
            {
                var entryId = StringValue(item, "entry_id", "ledger_entry_id", "id");
                if (!string.IsNullOrWhiteSpace(entryId))
                {
                    if (!seenEntryIds.Add(entryId)) continue;
                }
                combined.Add(item);
            }
        }

        return combined;
    }

    private async Task<IReadOnlyList<JsonElement>> GetCachedLedgerWindowAsync(
        string routeTemplate,
        string shopId,
        string apiKeyHeader,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken cancellationToken)
    {
        var minUnix = windowStart.ToUnixTimeSeconds();
        var maxUnix = windowEnd.ToUnixTimeSeconds();
        var cacheKey = $"{_options.ApiBaseUrl}:{shopId}:{minUnix}:{maxUnix}";
        var now = DateTimeOffset.UtcNow;

        if (_ledgerWindowCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > now && !cached.Task.IsFaulted && !cached.Task.IsCanceled)
        {
            return await cached.Task;
        }

        var ttl = windowEnd < now.AddDays(-2) ? TimeSpan.FromMinutes(30) : TimeSpan.FromSeconds(15);
        var task = FetchRawWindowElementsAsync(routeTemplate, shopId, apiKeyHeader, minUnix, maxUnix, cancellationToken);
        _ledgerWindowCache[cacheKey] = (now.Add(ttl), task);

        try
        {
            return await task;
        }
        catch
        {
            _ledgerWindowCache.TryRemove(cacheKey, out _);
            throw;
        }
    }

    private async Task<IReadOnlyList<JsonElement>> FetchRawWindowElementsAsync(
        string routeTemplate,
        string shopId,
        string apiKeyHeader,
        long minUnix,
        long maxUnix,
        CancellationToken cancellationToken)
    {
        var elements = new List<JsonElement>();
        const int limit = 100;
        var route = string.Format(CultureInfo.InvariantCulture, routeTemplate, Uri.EscapeDataString(shopId));

        for (var offset = 0; ; offset += limit)
        {
            var uri = $"{_options.ApiBaseUrl.TrimEnd('/')}/{route}?limit={limit}&offset={offset}&min_created={minUnix}&max_created={maxUnix}";
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("X-Etsy-Shop-Id", shopId);
            request.Headers.Add("x-api-key", apiKeyHeader);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await CreateApiExceptionAsync(response, cancellationToken);

            using var document = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Etsy API boş yanıt döndürdü.");
            if (!document.RootElement.TryGetProperty("results", out var items) || items.ValueKind != JsonValueKind.Array)
                break;

            var count = 0;
            foreach (var item in items.EnumerateArray())
            {
                count++;
                elements.Add(item.Clone());
            }
            if (count < limit) break;
        }

        return elements;
    }

    private async Task<IReadOnlyList<T>> GetSingleRangePagedAsync<T>(string routeTemplate, string shopId, DateTimeOffset startDate, DateTimeOffset endDate, Func<JsonElement, string, T?> parser, CancellationToken cancellationToken)
        where T : class
    {
        var results = new List<T>();
        const int limit = 100;
        for (var offset = 0; ; offset += limit)
        {
            var route = string.Format(CultureInfo.InvariantCulture, routeTemplate, Uri.EscapeDataString(shopId));
            var uri = $"{_options.ApiBaseUrl.TrimEnd('/')}/{route}?limit={limit}&offset={offset}&min_created={startDate.ToUnixTimeSeconds()}&max_created={endDate.ToUnixTimeSeconds()}";
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("X-Etsy-Shop-Id", shopId);
            request.Headers.Add("x-api-key", await ResolveApiKeyHeaderAsync(shopId, cancellationToken));
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await CreateApiExceptionAsync(response, cancellationToken);

            using var document = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Etsy API boş yanıt döndürdü.");
            if (!document.RootElement.TryGetProperty("results", out var items) || items.ValueKind != JsonValueKind.Array)
                break;

            var count = 0;
            foreach (var item in items.EnumerateArray())
            {
                count++;
                var parsed = parser(item, shopId);
                if (parsed is not null) results.Add(parsed);
            }
            if (count < limit) break;
        }
        return results;
    }

    private static EtsyBankPayout? ParsePayout(JsonElement item, string shopId)
    {
        var reference = StringValue(item, "entry_id", "ledger_entry_id", "id");
        if (string.IsNullOrWhiteSpace(reference)) return null;

        var rawType = (StringValue(item, "ledger_type", "type") ?? string.Empty).ToLowerInvariant();
        var rawRefType = (StringValue(item, "reference_type") ?? string.Empty).ToLowerInvariant();
        var desc = (StringValue(item, "description") ?? string.Empty).ToLowerInvariant();

        var isDeposit = (rawType.Contains("deposit") || rawRefType.Contains("deposit") || desc.Contains("deposit") || desc.Contains("payout") || desc.Contains("disburse"))
                        && !desc.Contains("fee") && !rawType.Contains("fee");

        if (!isDeposit) return null;

        return new EtsyBankPayout(
            shopId,
            reference,
            DateValue(item, "create_date", "created_timestamp", "created", "date"),
            Math.Abs(LedgerAmountToUsd(item)),
            StringValue(item, "currency", "currency_code") ?? "USD",
            DecimalNullable(item, "exchange_rate"),
            StringValue(item, "status") ?? "completed",
            StringValue(item, "description") ?? "Etsy payout");
    }

    private static EtsyFinancialTransaction? ParseTransaction(JsonElement item, string shopId)
    {
        var reference = StringValue(item, "transaction_id", "id");
        if (string.IsNullOrWhiteSpace(reference)) return null;
        return new EtsyFinancialTransaction(shopId, reference, DateValue(item, "created_timestamp", "created"), DecimalValue(item, "price", "amount"), DecimalValue(item, "fees", "fee"), 0, 0, 0, DecimalValue(item, "shipping_cost"), DecimalValue(item, "refunded"), StringValue(item, "currency_code", "currency") ?? "USD");
    }

    private static EtsyOrderCostAlert? ParseOrder(JsonElement item, string shopId)
    {
        var id = StringValue(item, "receipt_id", "receiptId", "id");
        if (string.IsNullOrWhiteSpace(id)) return null;
        var isOpen = !BoolValue(item, "is_shipped") && !BoolValue(item, "is_canceled");
        return isOpen ? new EtsyOrderCostAlert(shopId, id, DateValue(item, "created_timestamp", "created"), StringValue(item, "currency_code", "currency") ?? "USD", DecimalValue(item, "grandtotal", "total"), null, null, "Açık siparişin ürün/kargo maliyeti yerel kayıtlarda bulunamadı.") : null;
    }

    private static EtsyDashboardReceipt? ParseDashboardReceipt(JsonElement item, string shopId)
    {
        if (!item.TryGetProperty("receipt_id", out var idProperty) || !idProperty.TryGetInt64(out var receiptId) || receiptId <= 0)
            return null;

        var items = new List<EtsyDashboardReceiptItem>();
        if (item.TryGetProperty("transactions", out var transactions) && transactions.ValueKind == JsonValueKind.Array)
        {
            foreach (var transaction in transactions.EnumerateArray())
            {
                items.Add(new EtsyDashboardReceiptItem(
                    StringValue(transaction, "title") ?? string.Empty,
                    LongValue(transaction, "listing_id"),
                    (int)LongValue(transaction, "quantity")));
            }
        }

        var status = StringValue(item, "status") ?? string.Empty;
        var grandTotal = MoneyValue(item, "grandtotal");

        var refundedAmount = 0m;
        if (item.TryGetProperty("refunds", out var refunds) && refunds.ValueKind == JsonValueKind.Array)
        {
            foreach (var refund in refunds.EnumerateArray())
            {
                refundedAmount += MoneyValue(refund, "amount");
            }
        }
        else if (item.TryGetProperty("refund_amount", out _))
        {
            refundedAmount = MoneyValue(item, "refund_amount");
        }

        var isCanceled = status.Contains("cancel", StringComparison.OrdinalIgnoreCase)
            || status.Contains("refund", StringComparison.OrdinalIgnoreCase)
            || BoolValue(item, "is_canceled")
            || BoolValue(item, "was_canceled")
            || BoolValue(item, "is_refunded")
            || BoolValue(item, "was_refunded")
            || (grandTotal > 0 && refundedAmount >= grandTotal);

        var buyerName = StringValue(item, "name", "buyer_name");
        var buyerEmail = StringValue(item, "buyer_email");
        var uid = LongValue(item, "buyer_user_id");
        long? buyerUserId = uid > 0 ? uid : null;

        return new EtsyDashboardReceipt(
            receiptId,
            DateValue(item, "created_timestamp", "create_timestamp"),
            BoolValue(item, "is_paid"),
            isCanceled,
            BoolValue(item, "is_from_offsite_ads"),
            StringValue(item, "currency_code", "currency") ?? "USD",
            grandTotal,
            MoneyValue(item, "subtotal"),
            MoneyValue(item, "total_shipping_cost"),
            MoneyValue(item, "discount_amt"),
            MoneyValue(item, "total_tax_cost", "total_vat_cost"),
            refundedAmount,
            items,
            buyerName,
            buyerUserId,
            buyerEmail,
            status);
    }

    private static EtsyDashboardLedgerFee? ParseDashboardLedgerFee(JsonElement item, string shopId)
    {
        var rawType = (StringValue(item, "ledger_type", "type") ?? string.Empty).ToLowerInvariant();
        var rawReferenceType = (StringValue(item, "reference_type") ?? string.Empty).ToLowerInvariant();
        var description = StringValue(item, "description") ?? string.Empty;
        var lowerDescription = description.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(rawType) && string.IsNullOrWhiteSpace(description)) return null;

        var referenceId = 0L;
        if (item.TryGetProperty("reference_id", out var referenceProperty))
        {
            if (referenceProperty.ValueKind == JsonValueKind.Number)
                referenceProperty.TryGetInt64(out referenceId);
            else if (referenceProperty.ValueKind == JsonValueKind.String)
                long.TryParse(referenceProperty.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out referenceId);
        }

        // Masaustu MapEntry ile ayni siniflandirma: tur alanlarindan veya aciklamadan "offsite" gecen kayitlar dis reklam kesintisidir.
        var kind = rawType.Contains("offsite") || rawReferenceType.Contains("offsite") || lowerDescription.Contains("offsite")
            ? "offsite_ads"
            : rawType;

        return new EtsyDashboardLedgerFee(
            kind,
            referenceId,
            description,
            LedgerAmountToUsd(item));
    }

    private static EtsyLedgerEntryDetail? ParseLedgerEntryDetail(JsonElement item, string shopId)
    {
        // Masaustu FinancialReportService.MapEntry birebir portu (parite: docs/finans-motoru-ve-parite.md)
        long entryId = item.TryGetProperty("entry_id", out var propEntryId) && propEntryId.ValueKind == JsonValueKind.Number ? propEntryId.GetInt64() : 0;
        var rawType = (StringValue(item, "ledger_type", "type") ?? string.Empty).ToLowerInvariant();
        var rawRefType = (StringValue(item, "reference_type") ?? string.Empty).ToLowerInvariant();
        var desc = (StringValue(item, "description") ?? string.Empty).ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(rawType) && string.IsNullOrWhiteSpace(desc)) return null;

        var createdAt = DateValue(item, "create_date", "created_timestamp", "created");

        long refId = 0;
        if (item.TryGetProperty("reference_id", out var propRefId))
        {
            if (propRefId.ValueKind == JsonValueKind.Number)
            {
                refId = propRefId.GetInt64();
            }
            else if (propRefId.ValueKind == JsonValueKind.String && long.TryParse(propRefId.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedRef))
            {
                refId = parsedRef;
            }
        }

        if (refId == 0 && !string.IsNullOrWhiteSpace(desc))
        {
            var match = System.Text.RegularExpressions.Regex.Match(desc, @"#?(\d{9,11})");
            if (match.Success && long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedFromDesc))
            {
                refId = parsedFromDesc;
            }
        }

        decimal amount = 0m;
        decimal netAmount = 0m;
        if (item.TryGetProperty("amount", out var amt) && amt.ValueKind == JsonValueKind.Number && amt.TryGetDecimal(out var rawCents))
        {
            amount = rawCents / 100m;
            netAmount = amount;
        }

        if ((item.TryGetProperty("net", out var netProp) || item.TryGetProperty("net_amount", out netProp)) && netProp.ValueKind == JsonValueKind.Number)
        {
            netAmount = netProp.GetDecimal() / 100m;
        }

        var currency = StringValue(item, "currency_code", "currency");
        if (string.IsNullOrWhiteSpace(currency) && item.TryGetProperty("money", out var moneyEl))
        {
            currency = StringValue(moneyEl, "currency_code");
        }
        currency ??= "USD";

        var rate = HistoricalExchangeRateProvider.GetRateForDate(createdAt.UtcDateTime, 48.25m);

        if (currency.Equals("TRY", StringComparison.OrdinalIgnoreCase) || currency.Equals("TL", StringComparison.OrdinalIgnoreCase))
        {
            if (rate > 0) { amount /= rate; netAmount /= rate; }
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

        var type = EtsyLedgerFinancialEngine.ClassifyEntry(rawType, rawRefType, desc);
        var amountTry = Math.Round(amount * rate, 2);

        return new EtsyLedgerEntryDetail(type, rawType, rawRefType, desc, amount, netAmount, amountTry, currency, createdAt, rate, refId, entryId);
    }

    /// <summary>
    /// Etsy odeme hesabi defter tutarlarini USD'ye cevirir: tutarlar cent cinsinden tam sayidir (or. -547 => -5.47);
    /// TRY tutarlar masaustu gibi gunun tarihsel kuru ile bolunur, EUR x1.1 ve GBP x1.3 ile yaklasiklanir.
    /// </summary>
    private static decimal LedgerAmountToUsd(JsonElement item)
    {
        if (!item.TryGetProperty("amount", out var amount) || amount.ValueKind != JsonValueKind.Number || !amount.TryGetDecimal(out var raw))
            return 0m;

        var value = raw / 100m;
        var currency = (StringValue(item, "currency", "currency_code") ?? "USD").ToUpperInvariant();

        if (currency is "TRY" or "TL")
        {
            var entryDate = DateValue(item, "create_date", "created_timestamp");
            var rate = HistoricalExchangeRateProvider.GetRateForDate(entryDate.UtcDateTime, 48.25m);
            if (rate > 0) value /= rate;
        }
        else if (currency == "EUR")
        {
            value *= 1.1m;
        }
        else if (currency == "GBP")
        {
            value *= 1.3m;
        }

        return value;
    }

    private static string? StringValue(JsonElement element, params string[] names)
    {
        foreach (var name in names)
            if (element.TryGetProperty(name, out var value))
                return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        return null;
    }

    private static decimal DecimalValue(JsonElement element, params string[] names) => DecimalNullable(element, names) ?? 0;

    private static decimal? DecimalNullable(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("amount", out var amount) && amount.TryGetDecimal(out number)) return number / 100m;
            if (decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out number)) return number;
        }
        return null;
    }

    private static DateTimeOffset DateValue(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var unix)) return DateTimeOffset.FromUnixTimeSeconds(unix);
            if (DateTimeOffset.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)) return date;
        }
        return DateTimeOffset.UtcNow;
    }

    private static bool BoolValue(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static long LongValue(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
        return long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static decimal MoneyValue(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value)) continue;

            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("amount", out var amountElement) && amountElement.ValueKind == JsonValueKind.Number && amountElement.TryGetDecimal(out var rawAmount))
            {
                var divisor = value.TryGetProperty("divisor", out var divisorElement) && divisorElement.TryGetDecimal(out var resolvedDivisor) && resolvedDivisor != 0 ? resolvedDivisor : 100m;
                return rawAmount / divisor;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
            if (decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        }

        return 0m;
    }

    private static async Task<Exception> CreateApiExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        return new HttpRequestException($"Etsy API {(int)response.StatusCode} ({response.StatusCode}): {detail}", null, response.StatusCode);
    }
}
