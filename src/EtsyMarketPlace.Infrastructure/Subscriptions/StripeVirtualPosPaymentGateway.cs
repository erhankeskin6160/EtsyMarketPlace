using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Subscriptions;
using EtsyMarketPlace.Domain.Subscriptions.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EtsyMarketPlace.Infrastructure.Subscriptions;

/// <summary>
/// Stripe Checkout (canlı anahtar varsa) veya yerleşik Stripe Sanal POS yönlendirme/provizyon geçidi.
/// </summary>
public sealed class StripeVirtualPosPaymentGateway : ISubscriptionPaymentGateway
{
    private static readonly HashSet<string> DeclinedTestCards = new(StringComparer.Ordinal)
    {
        "4000000000000002",
        "4000002500003155",
        "4000000000000069",
        "4000000000000119"
    };

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeVirtualPosPaymentGateway> _logger;

    public StripeVirtualPosPaymentGateway(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<StripeVirtualPosPaymentGateway> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PaymentGatewaySessionCreateResult> CreateCheckoutSessionAsync(
        PaymentGatewaySessionCreateInput input,
        CancellationToken cancellationToken = default)
    {
        var stripeSecretKey = _configuration["Stripe:SecretKey"]?.Trim();
        var expiresAt = DateTime.UtcNow.AddMinutes(30);

        if (!string.IsNullOrWhiteSpace(stripeSecretKey) &&
            stripeSecretKey.StartsWith("sk_", StringComparison.OrdinalIgnoreCase))
        {
            var liveResult = await TryCreateLiveStripeCheckoutAsync(stripeSecretKey, input, expiresAt, cancellationToken);
            if (liveResult is not null)
            {
                return liveResult;
            }
        }

        var sessionId = $"cs_vpos_{Guid.NewGuid():N}";
        var baseUrl = string.IsNullOrWhiteSpace(input.ReturnBaseUrl)
            ? string.Empty
            : input.ReturnBaseUrl.Trim().TrimEnd('/');

        var checkoutUrl = $"{baseUrl}/settings/subscription?checkoutSessionId={Uri.EscapeDataString(sessionId)}";

        return new PaymentGatewaySessionCreateResult(
            Success: true,
            SessionId: sessionId,
            CheckoutUrl: checkoutUrl,
            Mode: "VirtualPos",
            Provider: "StripeVirtualPos",
            ExpiresAtUtc: expiresAt);
    }

    public async Task<VirtualPosChargeResult> ProcessVirtualPosPaymentAsync(
        SubscriptionTransaction transaction,
        ConfirmVirtualPosPaymentRequest cardRequest,
        CancellationToken cancellationToken = default)
    {
        var stripeSecretKey = _configuration["Stripe:SecretKey"]?.Trim();
        if (!string.IsNullOrWhiteSpace(stripeSecretKey) &&
            stripeSecretKey.StartsWith("sk_", StringComparison.OrdinalIgnoreCase) &&
            (transaction.SessionId.StartsWith("cs_live_", StringComparison.OrdinalIgnoreCase) ||
             transaction.SessionId.StartsWith("cs_test_", StringComparison.OrdinalIgnoreCase)))
        {
            var verified = await VerifyLiveStripeSessionAsync(stripeSecretKey, transaction.SessionId, cancellationToken);
            if (verified.Success)
            {
                return verified;
            }
        }

        var digits = new string((cardRequest.CardNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length is < 13 or > 19 || !PassesLuhnCheck(digits))
        {
            return new VirtualPosChargeResult(
                false,
                string.Empty,
                string.Empty,
                "Kart numarası geçersiz (Luhn doğrulamasından geçemedi).");
        }

        if (DeclinedTestCards.Contains(digits))
        {
            return new VirtualPosChargeResult(
                false,
                MaskCard(digits),
                string.Empty,
                "Kart provizyonu banka tarafından reddedildi (Yetersiz bakiye veya işlem kısıtı).");
        }

        if (!IsExpiryValid(cardRequest.ExpiryMonth, cardRequest.ExpiryYear))
        {
            return new VirtualPosChargeResult(
                false,
                MaskCard(digits),
                string.Empty,
                "Kart son kullanma tarihi geçmiş veya geçersiz.");
        }

        var cvcDigits = new string((cardRequest.Cvc ?? string.Empty).Where(char.IsDigit).ToArray());
        if (cvcDigits.Length is < 3 or > 4)
        {
            return new VirtualPosChargeResult(
                false,
                MaskCard(digits),
                string.Empty,
                "CVC güvenlik kodu 3 veya 4 haneli olmalıdır.");
        }

        var masked = MaskCard(digits);
        var chargeRef = $"ch_vpos_{Guid.NewGuid().ToString("N")[..16]}";
        return new VirtualPosChargeResult(true, masked, chargeRef);
    }

    private async Task<PaymentGatewaySessionCreateResult?> TryCreateLiveStripeCheckoutAsync(
        string secretKey,
        PaymentGatewaySessionCreateInput input,
        DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        try
        {
            var returnBase = string.IsNullOrWhiteSpace(input.ReturnBaseUrl)
                ? "http://localhost:4200"
                : input.ReturnBaseUrl.Trim().TrimEnd('/');

            var unitAmountMinor = string.Equals(input.Currency, "TRY", StringComparison.OrdinalIgnoreCase)
                ? (long)Math.Round(input.AmountTry * 100m)
                : (long)Math.Round(input.AmountUsd * 100m);

            var interval = string.Equals(input.BillingCycle, "Yearly", StringComparison.OrdinalIgnoreCase)
                ? "year"
                : "month";

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/checkout/sessions");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["mode"] = "subscription",
                ["customer_email"] = input.UserEmail,
                ["success_url"] = $"{returnBase}/settings/subscription?stripe_status=success&session_id={{CHECKOUT_SESSION_ID}}",
                ["cancel_url"] = $"{returnBase}/settings/subscription?stripe_status=cancelled",
                ["client_reference_id"] = input.UserId,
                ["line_items[0][quantity]"] = "1",
                ["line_items[0][price_data][currency]"] = input.Currency.ToLowerInvariant(),
                ["line_items[0][price_data][unit_amount]"] = unitAmountMinor.ToString(CultureInfo.InvariantCulture),
                ["line_items[0][price_data][recurring][interval]"] = interval,
                ["line_items[0][price_data][product_data][name]"] = $"EtsyMarketPlace {input.Plan.Name} ({input.BillingCycle})",
                ["metadata[plan_id]"] = input.Plan.Id,
                ["metadata[invoice_number]"] = input.InvoiceNumber
            });

            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                var errBody = await resp.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Stripe canlı Checkout oturumu oluşturulamadı ({Status}): {Body}", resp.StatusCode, errBody);
                return null;
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var id = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            var url = doc.RootElement.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(url))
                return null;

            return new PaymentGatewaySessionCreateResult(
                Success: true,
                SessionId: id,
                CheckoutUrl: url,
                Mode: "StripeLive",
                Provider: "Stripe",
                ExpiresAtUtc: expiresAt);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Stripe API çağrısı başarısız oldu, dahili Sanal POS moduna geçiliyor.");
            return null;
        }
    }

    private async Task<VirtualPosChargeResult> VerifyLiveStripeSessionAsync(
        string secretKey,
        string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.stripe.com/v1/checkout/sessions/{Uri.EscapeDataString(sessionId)}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                return new VirtualPosChargeResult(false, string.Empty, string.Empty, "Stripe oturumu doğrulanamadı.");
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var paymentStatus = doc.RootElement.TryGetProperty("payment_status", out var ps) ? ps.GetString() : null;

            if (string.Equals(paymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
            {
                return new VirtualPosChargeResult(true, "Stripe Hosted", sessionId);
            }

            return new VirtualPosChargeResult(false, string.Empty, string.Empty, $"Stripe ödeme durumu henüz tamamlanmadı: {paymentStatus}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new VirtualPosChargeResult(false, string.Empty, string.Empty, $"Stripe doğrulama hatası: {ex.Message}");
        }
    }

    public static bool PassesLuhnCheck(string digits)
    {
        if (string.IsNullOrWhiteSpace(digits)) return false;
        var sum = 0;
        var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';
            if (n < 0 || n > 9) return false;
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alternate = !alternate;
        }
        return sum % 10 == 0;
    }

    private static bool IsExpiryValid(string? monthStr, string? yearStr)
    {
        if (!int.TryParse(monthStr?.Trim(), out var month) || month is < 1 or > 12)
            return false;

        if (!int.TryParse(yearStr?.Trim(), out var year))
            return false;

        if (year < 100) year += 2000;
        if (year < 2000 || year > 2099) return false;

        var now = DateTime.UtcNow;
        var expiryEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month), 23, 59, 59, DateTimeKind.Utc);
        return expiryEnd >= now;
    }

    private static string MaskCard(string digits)
        => digits.Length >= 4
            ? $"**** **** **** {digits[^4..]}"
            : "****";
}
