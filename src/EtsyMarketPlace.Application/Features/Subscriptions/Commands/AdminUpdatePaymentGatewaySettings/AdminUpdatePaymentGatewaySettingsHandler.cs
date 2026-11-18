using System.Net;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Subscriptions;
using EtsyMarketPlace.Domain.Subscriptions.Entities;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.AdminUpdatePaymentGatewaySettings;

public sealed class AdminUpdatePaymentGatewaySettingsHandler(
    ISubscriptionRepository subscriptionRepository,
    IUserRepository userRepository,
    ISubscriptionPaymentGateway paymentGateway)
    : IRequestHandler<AdminUpdatePaymentGatewaySettingsCommand, ServiceResponse>
{
    private const string IyzicoSandboxUrl = "https://sandbox-api.iyzipay.com";
    private const string IyzicoLiveUrl = "https://api.iyzipay.com";

    public async Task<ServiceResponse> Handle(AdminUpdatePaymentGatewaySettingsCommand request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var existing = await subscriptionRepository.GetPaymentGatewayConfigAsync(cancellationToken)
                       ?? new PaymentGatewayConfig();

        var provider = NormalizeProvider(request.Request.ActiveProvider);
        var isSandbox = request.Request.IsSandbox;

        var iyzicoApiKey = KeepOrUpdate(request.Request.IyzicoApiKey, existing.IyzicoApiKey);
        var iyzicoSecretKey = KeepOrUpdateSecret(request.Request.IyzicoSecretKey, existing.IyzicoSecretKey);
        var iyzicoBaseUrl = ResolveIyzicoBaseUrl(request.Request.IyzicoBaseUrl, existing.IyzicoBaseUrl, isSandbox);

        var paytrMerchantId = KeepOrUpdate(request.Request.PaytrMerchantId, existing.PaytrMerchantId);
        var paytrMerchantKey = KeepOrUpdateSecret(request.Request.PaytrMerchantKey, existing.PaytrMerchantKey);
        var paytrMerchantSalt = KeepOrUpdateSecret(request.Request.PaytrMerchantSalt, existing.PaytrMerchantSalt);
        var stripeSecretKey = KeepOrUpdateSecret(request.Request.StripeSecretKey, existing.StripeSecretKey);

        if (provider == "PayTR" &&
            (string.IsNullOrWhiteSpace(paytrMerchantId) ||
             string.IsNullOrWhiteSpace(paytrMerchantKey) ||
             string.IsNullOrWhiteSpace(paytrMerchantSalt)))
        {
            return ServiceResponse.CreateError(
                "PayTR sağlayıcısını aktifleştirmek için Merchant ID, Merchant Key ve Merchant Salt alanlarının tümü girilmelidir.",
                HttpStatusCode.BadRequest);
        }

        if (provider == "Iyzico" &&
            (string.IsNullOrWhiteSpace(iyzicoApiKey) || string.IsNullOrWhiteSpace(iyzicoSecretKey)))
        {
            return ServiceResponse.CreateError(
                "İyzico sağlayıcısını aktifleştirmek için API Key ve Secret Key alanları girilmelidir.",
                HttpStatusCode.BadRequest);
        }

        if (provider == "Stripe" &&
            !stripeSecretKey.StartsWith("sk_", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResponse.CreateError(
                "Stripe sağlayıcısını aktifleştirmek için 'sk_test_...' veya 'sk_live_...' ile başlayan geçerli bir Secret Key giriniz.",
                HttpStatusCode.BadRequest);
        }

        var next = new PaymentGatewayConfig
        {
            Id = "default",
            ActiveProvider = provider,
            IsSandbox = isSandbox,
            IyzicoApiKey = iyzicoApiKey,
            IyzicoSecretKey = iyzicoSecretKey,
            IyzicoBaseUrl = iyzicoBaseUrl,
            PaytrMerchantId = paytrMerchantId,
            PaytrMerchantKey = paytrMerchantKey,
            PaytrMerchantSalt = paytrMerchantSalt,
            StripeSecretKey = stripeSecretKey,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await subscriptionRepository.UpsertPaymentGatewayConfigAsync(next, cancellationToken);

        await SubscriptionLifecycleHelper.TryAddAuditLogAsync(
            userRepository,
            request.AdminId ?? "admin",
            request.AdminName ?? "Admin",
            "UpdatePaymentGatewaySettings",
            $"Ödeme altyapısı güncellendi: Aktif Sağlayıcı = {provider}, Ortam = {(isSandbox ? "Sandbox" : "Canlı")}",
            request.RemoteIpAddress,
            cancellationToken);

        var summary = await paymentGateway.GetSettingsSummaryAsync(cancellationToken);
        return ServiceResponse.CreateSuccess("Ödeme altyapısı ayarları kaydedildi.", summary);
    }

    private static string NormalizeProvider(string? raw)
    {
        return (raw?.Trim().ToLowerInvariant()) switch
        {
            "iyzico" => "Iyzico",
            "paytr" => "PayTR",
            "stripe" => "Stripe",
            _ => "Unconfigured"
        };
    }

    private static string KeepOrUpdate(string? submitted, string current)
        => submitted is null ? current : submitted.Trim();

    private static string KeepOrUpdateSecret(string? submitted, string current)
        => string.IsNullOrWhiteSpace(submitted) ? current : submitted.Trim();

    private static string ResolveIyzicoBaseUrl(string? submitted, string current, bool isSandbox)
    {
        var candidate = submitted?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(candidate))
            return isSandbox ? IyzicoSandboxUrl : IyzicoLiveUrl;

        if (!isSandbox && string.Equals(candidate, IyzicoSandboxUrl, StringComparison.OrdinalIgnoreCase))
            return IyzicoLiveUrl;

        if (isSandbox && string.Equals(candidate, IyzicoLiveUrl, StringComparison.OrdinalIgnoreCase))
            return IyzicoSandboxUrl;

        return candidate;
    }
}
