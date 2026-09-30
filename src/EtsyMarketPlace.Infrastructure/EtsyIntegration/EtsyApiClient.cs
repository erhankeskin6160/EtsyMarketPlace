using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EtsyMarketPlace.Application.EtsyIntegration;
using Microsoft.Extensions.Options;

namespace EtsyMarketPlace.Infrastructure.EtsyIntegration;

public sealed class EtsyApiClient(HttpClient httpClient, IOptions<EtsyApiOptions> options) : IEtsyDataClient
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

    private async Task<IReadOnlyList<T>> GetPagedAsync<T>(string routeTemplate, string shopId, DateTimeOffset startDate, DateTimeOffset endDate, Func<JsonElement, string, T?> parser, CancellationToken cancellationToken)
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
            request.Headers.Add("x-api-key", $"{_options.ApiKey}:{_options.SharedSecret}");
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
        var reference = StringValue(item, "ledger_entry_id", "id");
        if (string.IsNullOrWhiteSpace(reference)) return null;
        return new EtsyBankPayout(shopId, reference, DateValue(item, "created", "date"), DecimalValue(item, "amount", "amount_money"), StringValue(item, "currency", "currency_code") ?? "USD", DecimalNullable(item, "exchange_rate"), StringValue(item, "status") ?? "completed", StringValue(item, "description") ?? "Etsy payout");
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

    private static async Task<Exception> CreateApiExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        return new HttpRequestException($"Etsy API {(int)response.StatusCode} ({response.StatusCode}): {detail}", null, response.StatusCode);
    }
}
