using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Subscriptions;
using EtsyMarketPlace.Domain.Subscriptions.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EtsyMarketPlace.Infrastructure.Subscriptions;

/// <summary>
/// İyzico Checkout Form (iFrame), PayTR Ortak Ödeme (iFrame), Stripe Checkout
/// ve yapılandırma yokken dahili Sanal POS akışını yöneten ödeme geçidi.
/// </summary>
public sealed class StripeVirtualPosPaymentGateway : ISubscriptionPaymentGateway
{
    private const string IyzicoSandboxDefaultUrl = "https://sandbox-api.iyzipay.com";
    private const string IyzicoLiveDefaultUrl = "https://api.iyzipay.com";
    private const string PaytrGetTokenUrl = "https://www.paytr.com/odeme/api/get-token";
    private const string PaytrIframeBaseUrl = "https://www.paytr.com/odeme/guvenli";

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
    private readonly ISubscriptionRepository? _repository;

    public StripeVirtualPosPaymentGateway(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<StripeVirtualPosPaymentGateway> logger,
        ISubscriptionRepository? repository = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository;
    }

    public async Task<PaymentGatewaySettingsDto> GetSettingsSummaryAsync(CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveEffectiveConfigAsync(cancellationToken);
        var c = resolved.Config;
        return new PaymentGatewaySettingsDto(
            ActiveProvider: c.ActiveProvider,
            IsSandbox: c.IsSandbox,
            IyzicoApiKey: c.IyzicoApiKey,
            HasIyzicoSecretKey: !string.IsNullOrWhiteSpace(c.IyzicoSecretKey),
            IyzicoSecretKeyMask: MaskSecret(c.IyzicoSecretKey),
            IyzicoBaseUrl: string.IsNullOrWhiteSpace(c.IyzicoBaseUrl)
                ? (c.IsSandbox ? IyzicoSandboxDefaultUrl : IyzicoLiveDefaultUrl)
                : c.IyzicoBaseUrl,
            PaytrMerchantId: c.PaytrMerchantId,
            HasPaytrMerchantKey: !string.IsNullOrWhiteSpace(c.PaytrMerchantKey),
            PaytrMerchantKeyMask: MaskSecret(c.PaytrMerchantKey),
            HasPaytrMerchantSalt: !string.IsNullOrWhiteSpace(c.PaytrMerchantSalt),
            PaytrMerchantSaltMask: MaskSecret(c.PaytrMerchantSalt),
            HasStripeSecretKey: !string.IsNullOrWhiteSpace(c.StripeSecretKey),
            StripeSecretKeyMask: MaskSecret(c.StripeSecretKey),
            ConfigSource: resolved.Source,
            UpdatedAtUtc: c.UpdatedAtUtc.ToString("O"));
    }

    public async Task<PaymentGatewaySessionCreateResult> CreateCheckoutSessionAsync(
        PaymentGatewaySessionCreateInput input,
        CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(30);
        var resolved = await ResolveEffectiveConfigAsync(cancellationToken);
        var cfg = resolved.Config;

        if (string.Equals(cfg.ActiveProvider, "PayTR", StringComparison.OrdinalIgnoreCase))
        {
            return await CreatePaytrIframeSessionAsync(cfg, input, expiresAt, cancellationToken);
        }

        if (string.Equals(cfg.ActiveProvider, "Iyzico", StringComparison.OrdinalIgnoreCase))
        {
            return await CreateIyzicoCheckoutSessionAsync(cfg, input, expiresAt, cancellationToken);
        }

        if (string.Equals(cfg.ActiveProvider, "Stripe", StringComparison.OrdinalIgnoreCase) &&
            cfg.StripeSecretKey.StartsWith("sk_", StringComparison.OrdinalIgnoreCase))
        {
            var liveResult = await TryCreateLiveStripeCheckoutAsync(cfg.StripeSecretKey, input, expiresAt, cancellationToken);
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
            Provider: "UnconfiguredVirtualPos",
            ExpiresAtUtc: expiresAt);
    }

    public async Task<VirtualPosChargeResult> ProcessVirtualPosPaymentAsync(
        SubscriptionTransaction transaction,
        ConfirmVirtualPosPaymentRequest cardRequest,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveEffectiveConfigAsync(cancellationToken);
        var stripeSecretKey = resolved.Config.StripeSecretKey;

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

    public async Task<PaymentCallbackVerifyResult> VerifyPaytrCallbackAsync(
        PaytrCallbackInput input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.MerchantOid) || string.IsNullOrWhiteSpace(input.Hash))
        {
            return new PaymentCallbackVerifyResult(false, false, input.MerchantOid ?? string.Empty, "PayTR",
                ErrorMessage: "PayTR bildirim parametreleri eksik (merchant_oid / hash).");
        }

        var resolved = await ResolveEffectiveConfigAsync(cancellationToken);
        var merchantKey = resolved.Config.PaytrMerchantKey;
        var merchantSalt = resolved.Config.PaytrMerchantSalt;

        if (string.IsNullOrWhiteSpace(merchantKey) || string.IsNullOrWhiteSpace(merchantSalt))
        {
            return new PaymentCallbackVerifyResult(false, false, input.MerchantOid, "PayTR",
                ErrorMessage: "PayTR Merchant Key / Salt yapılandırması tanımlı değil.");
        }

        var payload = string.Concat(input.MerchantOid, merchantSalt, input.Status, input.TotalAmount);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(merchantKey));
        var expectedHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));

        var expectedBytes = Encoding.UTF8.GetBytes(expectedHash);
        var actualBytes = Encoding.UTF8.GetBytes(input.Hash.Trim());
        var isValid = expectedBytes.Length == actualBytes.Length &&
                      CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);

        if (!isValid)
        {
            _logger.LogWarning("PayTR callback imza doğrulaması başarısız oldu: merchant_oid={Oid}", input.MerchantOid);
            return new PaymentCallbackVerifyResult(false, false, input.MerchantOid, "PayTR",
                ErrorMessage: "PayTR callback imza (hash) doğrulaması başarısız.");
        }

        var isPaid = string.Equals(input.Status, "success", StringComparison.OrdinalIgnoreCase);
        return new PaymentCallbackVerifyResult(
            IsSignatureValid: true,
            IsPaid: isPaid,
            SessionId: input.MerchantOid,
            Provider: "PayTR",
            CardMaskedNumber: "PayTR 3D Secure",
            ErrorMessage: isPaid ? null : (input.FailedReasonMsg ?? "PayTR ödemesi başarısız oldu."));
    }

    public async Task<PaymentCallbackVerifyResult> VerifyIyzicoCallbackAsync(
        IyzicoCallbackInput input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Token) || string.IsNullOrWhiteSpace(input.SessionId))
        {
            return new PaymentCallbackVerifyResult(false, false, input.SessionId ?? string.Empty, "Iyzico",
                ErrorMessage: "İyzico token veya sessionId eksik.");
        }

        var resolved = await ResolveEffectiveConfigAsync(cancellationToken);
        var cfg = resolved.Config;
        if (string.IsNullOrWhiteSpace(cfg.IyzicoApiKey) || string.IsNullOrWhiteSpace(cfg.IyzicoSecretKey))
        {
            return new PaymentCallbackVerifyResult(false, false, input.SessionId, "Iyzico",
                ErrorMessage: "İyzico API anahtarları tanımlı değil.");
        }

        const string uriPath = "/payment/iyzipos/checkoutform/auth/ecom/detail";
        var baseUrl = NormalizeIyzicoBaseUrl(cfg.IyzicoBaseUrl, cfg.IsSandbox);
        var bodyJson = JsonSerializer.Serialize(new
        {
            locale = "tr",
            conversationId = input.SessionId,
            token = input.Token
        });

        try
        {
            using var req = CreateSignedIyzicoRequest(HttpMethod.Post, baseUrl, uriPath, bodyJson, cfg.IyzicoApiKey, cfg.IyzicoSecretKey);
            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            var rawJson = await resp.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            var status = ReadJsonString(root, "status");
            var paymentStatus = ReadJsonString(root, "paymentStatus");
            var errMsg = ReadJsonString(root, "errorMessage");
            var bin = ReadJsonString(root, "binNumber");
            var lastFour = ReadJsonString(root, "lastFourDigits");

            var isApiOk = resp.IsSuccessStatusCode && string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);
            var isPaid = isApiOk && string.Equals(paymentStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase);
            var masked = !string.IsNullOrWhiteSpace(lastFour)
                ? (!string.IsNullOrWhiteSpace(bin) ? $"{bin} **** {lastFour}" : $"**** **** **** {lastFour}")
                : "İyzico 3D Secure";

            return new PaymentCallbackVerifyResult(
                IsSignatureValid: true,
                IsPaid: isPaid,
                SessionId: input.SessionId,
                Provider: "Iyzico",
                CardMaskedNumber: masked,
                ErrorMessage: isPaid ? null : (errMsg ?? $"İyzico ödeme durumu: {paymentStatus ?? status}"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "İyzico callback detay sorgusu sırasında hata oluştu: {Message}", ex.Message);
            return new PaymentCallbackVerifyResult(false, false, input.SessionId, "Iyzico",
                ErrorMessage: $"İyzico doğrulama hatası: {ex.Message}");
        }
    }

    private async Task<PaymentGatewaySessionCreateResult> CreatePaytrIframeSessionAsync(
        PaymentGatewayConfig cfg,
        PaymentGatewaySessionCreateInput input,
        DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cfg.PaytrMerchantId) ||
            string.IsNullOrWhiteSpace(cfg.PaytrMerchantKey) ||
            string.IsNullOrWhiteSpace(cfg.PaytrMerchantSalt))
        {
            return new PaymentGatewaySessionCreateResult(
                false, string.Empty, string.Empty, "Iframe", "PayTR", expiresAt,
                "PayTR Merchant ID, Key veya Salt bilgisi eksik. Admin Panelinden kontrol ediniz.");
        }

        // PayTR merchant_oid strictly requires alphanumeric characters only (no underscores or hyphens)
        var merchantOid = $"PAYTR{Guid.NewGuid():N}"[..28].ToUpperInvariant();
        var isTry = string.Equals(input.Currency, "TRY", StringComparison.OrdinalIgnoreCase);
        var amount = isTry ? input.AmountTry : input.AmountUsd;
        var minorAmount = ((long)Math.Round(amount * 100m)).ToString(CultureInfo.InvariantCulture);
        var paytrCurrency = isTry ? "TL" : "USD";
        var testMode = cfg.IsSandbox ? "1" : "0";
        var userIp = NormalizeClientIp(input.ClientIp);
        var email = string.IsNullOrWhiteSpace(input.UserEmail) ? "satis@etsymarketplace.local" : input.UserEmail.Trim();

        var priceStr = amount.ToString("0.00", CultureInfo.InvariantCulture);
        var basketJson = JsonSerializer.Serialize(new object[]
        {
            new object[] { $"EtsyMarketPlace {input.Plan.Name} ({input.BillingCycle})", priceStr, 1 }
        });
        var userBasket = Convert.ToBase64String(Encoding.UTF8.GetBytes(basketJson));

        const string noInstallment = "0";
        const string maxInstallment = "0";
        var hashStr = string.Concat(
            cfg.PaytrMerchantId.Trim(),
            userIp,
            merchantOid,
            email,
            minorAmount,
            userBasket,
            noInstallment,
            maxInstallment,
            paytrCurrency,
            testMode);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(cfg.PaytrMerchantKey.Trim()));
        var paytrToken = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(hashStr + cfg.PaytrMerchantSalt.Trim())));

        var returnBase = string.IsNullOrWhiteSpace(input.ReturnBaseUrl)
            ? "http://localhost:5288"
            : input.ReturnBaseUrl.Trim().TrimEnd('/');

        var form = new Dictionary<string, string>
        {
            ["merchant_id"] = cfg.PaytrMerchantId.Trim(),
            ["user_ip"] = userIp,
            ["merchant_oid"] = merchantOid,
            ["email"] = email,
            ["payment_amount"] = minorAmount,
            ["paytr_token"] = paytrToken,
            ["user_basket"] = userBasket,
            ["debug_on"] = cfg.IsSandbox ? "1" : "0",
            ["no_installment"] = noInstallment,
            ["max_installment"] = maxInstallment,
            ["user_name"] = string.IsNullOrWhiteSpace(input.Username) ? "Etsy Satıcı" : input.Username.Trim(),
            ["user_address"] = "Turkiye",
            ["user_phone"] = "05000000000",
            ["merchant_ok_url"] = $"{returnBase}/settings/subscription?payment_status=success&provider=PayTR&sessionId={Uri.EscapeDataString(merchantOid)}",
            ["merchant_fail_url"] = $"{returnBase}/settings/subscription?payment_status=failed&provider=PayTR&sessionId={Uri.EscapeDataString(merchantOid)}",
            ["timeout_limit"] = "30",
            ["currency"] = paytrCurrency,
            ["test_mode"] = testMode,
            ["lang"] = "tr"
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, PaytrGetTokenUrl)
            {
                Content = new FormUrlEncodedContent(form)
            };
            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            var rawBody = await resp.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var status = ReadJsonString(root, "status");
            var iframeToken = ReadJsonString(root, "token");
            var reason = ReadJsonString(root, "reason");

            if (resp.IsSuccessStatusCode &&
                string.Equals(status, "success", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(iframeToken))
            {
                return new PaymentGatewaySessionCreateResult(
                    Success: true,
                    SessionId: merchantOid,
                    CheckoutUrl: $"{PaytrIframeBaseUrl}/{Uri.EscapeDataString(iframeToken)}",
                    Mode: "Iframe",
                    Provider: "PayTR",
                    ExpiresAtUtc: expiresAt);
            }

            _logger.LogWarning("PayTR token oluşturulamadı ({Status}): {Body}", resp.StatusCode, rawBody);
            return new PaymentGatewaySessionCreateResult(
                false, merchantOid, string.Empty, "Iframe", "PayTR", expiresAt,
                $"PayTR ödeme oturumu başlatılamadı: {reason ?? $"HTTP {(int)resp.StatusCode}"}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "PayTR API çağrısında hata: {Message}", ex.Message);
            return new PaymentGatewaySessionCreateResult(
                false, merchantOid, string.Empty, "Iframe", "PayTR", expiresAt,
                $"PayTR bağlantı hatası: {ex.Message}");
        }
    }

    private async Task<PaymentGatewaySessionCreateResult> CreateIyzicoCheckoutSessionAsync(
        PaymentGatewayConfig cfg,
        PaymentGatewaySessionCreateInput input,
        DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cfg.IyzicoApiKey) || string.IsNullOrWhiteSpace(cfg.IyzicoSecretKey))
        {
            return new PaymentGatewaySessionCreateResult(
                false, string.Empty, string.Empty, "Iframe", "Iyzico", expiresAt,
                "İyzico API Key veya Secret Key bilgisi eksik. Admin Panelinden kontrol ediniz.");
        }

        var sessionId = $"IYZ{Guid.NewGuid():N}"[..24].ToUpperInvariant();
        var isTry = string.Equals(input.Currency, "TRY", StringComparison.OrdinalIgnoreCase);
        var amount = isTry ? input.AmountTry : input.AmountUsd;
        var priceStr = amount.ToString("0.00", CultureInfo.InvariantCulture);
        var currency = isTry ? "TRY" : "USD";
        var returnBase = string.IsNullOrWhiteSpace(input.ReturnBaseUrl)
            ? "http://localhost:5288"
            : input.ReturnBaseUrl.Trim().TrimEnd('/');

        var callbackUrl = $"{returnBase}/api/subscriptions/callback/iyzico?sessionId={Uri.EscapeDataString(sessionId)}";
        var baseUrl = NormalizeIyzicoBaseUrl(cfg.IyzicoBaseUrl, cfg.IsSandbox);
        const string uriPath = "/payment/iyzipos/checkoutform/initialize/auth/ecom";

        var displayName = string.IsNullOrWhiteSpace(input.Username) ? "Etsy Satici" : input.Username.Trim();
        var payload = new
        {
            locale = "tr",
            conversationId = sessionId,
            price = priceStr,
            paidPrice = priceStr,
            currency,
            basketId = input.InvoiceNumber,
            paymentGroup = "SUBSCRIPTION",
            callbackUrl,
            enabledInstallments = new[] { 1, 2, 3, 6, 9 },
            buyer = new
            {
                id = input.UserId,
                name = displayName,
                surname = "EtsyMarketPlace",
                gsmNumber = "+905000000000",
                email = string.IsNullOrWhiteSpace(input.UserEmail) ? "satis@etsymarketplace.local" : input.UserEmail.Trim(),
                identityNumber = "11111111111",
                registrationAddress = "Turkiye",
                ip = NormalizeClientIp(input.ClientIp),
                city = "Istanbul",
                country = "Turkey"
            },
            billingAddress = new
            {
                contactName = displayName,
                city = "Istanbul",
                country = "Turkey",
                address = "Turkiye"
            },
            basketItems = new[]
            {
                new
                {
                    id = input.Plan.Id,
                    name = $"EtsyMarketPlace {input.Plan.Name} ({input.BillingCycle})",
                    category1 = "SaaS Abonelik",
                    itemType = "VIRTUAL",
                    price = priceStr
                }
            }
        };

        var bodyJson = JsonSerializer.Serialize(payload);

        try
        {
            using var req = CreateSignedIyzicoRequest(HttpMethod.Post, baseUrl, uriPath, bodyJson, cfg.IyzicoApiKey.Trim(), cfg.IyzicoSecretKey.Trim());
            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            var rawBody = await resp.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var status = ReadJsonString(root, "status");
            var paymentPageUrl = ReadJsonString(root, "paymentPageUrl");
            var errMsg = ReadJsonString(root, "errorMessage");

            if (resp.IsSuccessStatusCode &&
                string.Equals(status, "success", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(paymentPageUrl))
            {
                return new PaymentGatewaySessionCreateResult(
                    Success: true,
                    SessionId: sessionId,
                    CheckoutUrl: paymentPageUrl,
                    Mode: "Iframe",
                    Provider: "Iyzico",
                    ExpiresAtUtc: expiresAt);
            }

            _logger.LogWarning("İyzico CheckoutForm başlatılamadı ({Status}): {Body}", resp.StatusCode, rawBody);
            return new PaymentGatewaySessionCreateResult(
                false, sessionId, string.Empty, "Iframe", "Iyzico", expiresAt,
                $"İyzico ödeme formu başlatılamadı: {errMsg ?? $"HTTP {(int)resp.StatusCode}"}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "İyzico API çağrısında hata: {Message}", ex.Message);
            return new PaymentGatewaySessionCreateResult(
                false, sessionId, string.Empty, "Iframe", "Iyzico", expiresAt,
                $"İyzico bağlantı hatası: {ex.Message}");
        }
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
            var id = ReadJsonString(doc.RootElement, "id");
            var url = ReadJsonString(doc.RootElement, "url");

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
            _logger.LogWarning(ex, "Stripe API çağrısı başarısız oldu, yedek akışa geçiliyor.");
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
            var paymentStatus = ReadJsonString(doc.RootElement, "payment_status");

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

    private async Task<(PaymentGatewayConfig Config, string Source)> ResolveEffectiveConfigAsync(CancellationToken cancellationToken)
    {
        PaymentGatewayConfig? dbCfg = null;
        if (_repository is not null)
        {
            try
            {
                dbCfg = await _repository.GetPaymentGatewayConfigAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Veritabanından ödeme geçidi ayarları okunamadı, appsettings.json kontrol ediliyor.");
            }
        }

        if (dbCfg is not null)
        {
            if (!string.Equals(dbCfg.ActiveProvider, "Unconfigured", StringComparison.OrdinalIgnoreCase))
            {
                return (dbCfg, "Admin Paneli (Veritabanı)");
            }

            // Admin açıkça Unconfigured seçtiyse veya sadece bilgi döndürmek için kayıtlı alanları koru
            if (HasAnyKey(dbCfg))
            {
                return (dbCfg, "Admin Paneli (Pasif)");
            }
        }

        var cfgPaytrId = _configuration["PayTR:MerchantId"]?.Trim() ?? string.Empty;
        var cfgPaytrKey = _configuration["PayTR:MerchantKey"]?.Trim() ?? string.Empty;
        var cfgPaytrSalt = _configuration["PayTR:MerchantSalt"]?.Trim() ?? string.Empty;
        var cfgIyzicoKey = _configuration["Iyzico:ApiKey"]?.Trim() ?? string.Empty;
        var cfgIyzicoSecret = _configuration["Iyzico:SecretKey"]?.Trim() ?? string.Empty;
        var cfgIyzicoUrl = _configuration["Iyzico:BaseUrl"]?.Trim() ?? IyzicoSandboxDefaultUrl;
        var cfgStripeKey = _configuration["Stripe:SecretKey"]?.Trim() ?? string.Empty;
        var cfgProvider = _configuration["PaymentGateway:ActiveProvider"]?.Trim();

        var isSandbox = !bool.TryParse(_configuration["PaymentGateway:IsSandbox"], out var parsedSandbox) || parsedSandbox;
        var resolvedProvider = !string.IsNullOrWhiteSpace(cfgProvider)
            ? cfgProvider
            : (!string.IsNullOrWhiteSpace(cfgPaytrId) && !string.IsNullOrWhiteSpace(cfgPaytrKey) && !string.IsNullOrWhiteSpace(cfgPaytrSalt)
                ? "PayTR"
                : (!string.IsNullOrWhiteSpace(cfgIyzicoKey) && !string.IsNullOrWhiteSpace(cfgIyzicoSecret)
                    ? "Iyzico"
                    : (cfgStripeKey.StartsWith("sk_", StringComparison.OrdinalIgnoreCase) ? "Stripe" : "Unconfigured")));

        var hasConfigValues = !string.Equals(resolvedProvider, "Unconfigured", StringComparison.OrdinalIgnoreCase);

        var combined = new PaymentGatewayConfig
        {
            Id = "default",
            ActiveProvider = resolvedProvider,
            IsSandbox = isSandbox,
            IyzicoApiKey = !string.IsNullOrWhiteSpace(dbCfg?.IyzicoApiKey) ? dbCfg.IyzicoApiKey : cfgIyzicoKey,
            IyzicoSecretKey = !string.IsNullOrWhiteSpace(dbCfg?.IyzicoSecretKey) ? dbCfg.IyzicoSecretKey : cfgIyzicoSecret,
            IyzicoBaseUrl = !string.IsNullOrWhiteSpace(dbCfg?.IyzicoBaseUrl) ? dbCfg.IyzicoBaseUrl : cfgIyzicoUrl,
            PaytrMerchantId = !string.IsNullOrWhiteSpace(dbCfg?.PaytrMerchantId) ? dbCfg.PaytrMerchantId : cfgPaytrId,
            PaytrMerchantKey = !string.IsNullOrWhiteSpace(dbCfg?.PaytrMerchantKey) ? dbCfg.PaytrMerchantKey : cfgPaytrKey,
            PaytrMerchantSalt = !string.IsNullOrWhiteSpace(dbCfg?.PaytrMerchantSalt) ? dbCfg.PaytrMerchantSalt : cfgPaytrSalt,
            StripeSecretKey = !string.IsNullOrWhiteSpace(dbCfg?.StripeSecretKey) ? dbCfg.StripeSecretKey : cfgStripeKey,
            UpdatedAtUtc = dbCfg?.UpdatedAtUtc ?? DateTime.UtcNow
        };

        var source = hasConfigValues
            ? "appsettings.json"
            : (dbCfg is not null ? "Admin Paneli (Henüz Aktif Sağlayıcı Seçilmedi)" : "Henüz Yapılandırılmadı");

        return (combined, source);
    }

    private static HttpRequestMessage CreateSignedIyzicoRequest(
        HttpMethod method,
        string baseUrl,
        string uriPath,
        string bodyJson,
        string apiKey,
        string secretKey)
    {
        var randomKey = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{Guid.NewGuid().ToString("N")[..6]}";
        var hmacPayload = string.Concat(randomKey, uriPath, bodyJson);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var signatureHex = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(hmacPayload))).ToLowerInvariant();
        var authParamStr = $"apiKey:{apiKey}&randomKey:{randomKey}&signature:{signatureHex}";
        var authBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(authParamStr));

        var req = new HttpRequestMessage(method, $"{baseUrl.TrimEnd('/')}{uriPath}")
        {
            Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Authorization", $"IYZWSv2 {authBase64}");
        req.Headers.TryAddWithoutValidation("x-iyzi-rnd", randomKey);
        return req;
    }

    private static bool HasAnyKey(PaymentGatewayConfig cfg)
        => !string.IsNullOrWhiteSpace(cfg.IyzicoApiKey) ||
           !string.IsNullOrWhiteSpace(cfg.IyzicoSecretKey) ||
           !string.IsNullOrWhiteSpace(cfg.PaytrMerchantId) ||
           !string.IsNullOrWhiteSpace(cfg.PaytrMerchantKey) ||
           !string.IsNullOrWhiteSpace(cfg.StripeSecretKey);

    private static string NormalizeIyzicoBaseUrl(string? rawUrl, bool isSandbox)
    {
        var trimmed = rawUrl?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(trimmed))
            return trimmed;
        return isSandbox ? IyzicoSandboxDefaultUrl : IyzicoLiveDefaultUrl;
    }

    private static string NormalizeClientIp(string? rawIp)
    {
        var ip = rawIp?.Trim();
        if (string.IsNullOrWhiteSpace(ip) || ip == "::1")
            return "127.0.0.1";
        return ip;
    }

    private static string? ReadJsonString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static string MaskSecret(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return string.Empty;
        var s = secret.Trim();
        return s.Length <= 4 ? "••••" : $"••••••••{s[^4..]}";
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
