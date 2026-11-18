using System.Net;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Subscriptions;
using EtsyMarketPlace.Domain.Subscriptions;
using EtsyMarketPlace.Domain.Subscriptions.Entities;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.CreateCheckoutSession;

public sealed class CreateCheckoutSessionHandler(
    ISubscriptionRepository subscriptionRepository,
    IUserRepository userRepository,
    ISubscriptionPaymentGateway paymentGateway)
    : IRequestHandler<CreateCheckoutSessionCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(CreateCheckoutSessionCommand request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var plan = await SubscriptionLifecycleHelper.ResolvePlanAsync(
            subscriptionRepository,
            request.Request.PlanId,
            cancellationToken);

        if (plan is null || !plan.IsActive)
        {
            return ServiceResponse.CreateNotFound("Seçilen abonelik paketi bulunamadı veya aktif değil.");
        }

        var user = await SubscriptionLifecycleHelper.TryFindUserAsync(userRepository, request.UserId, cancellationToken);
        var isAdmin = SubscriptionLifecycleHelper.IsAdminRole(request.RoleHint);
        if (user is null && !isAdmin)
        {
            return ServiceResponse.CreateNotFound("Abonelik başlatılacak kullanıcı bulunamadı.");
        }

        var username = user?.Username ?? request.UsernameHint ?? "admin";
        var email = user?.Email ?? request.EmailHint ?? "admin@etsymarketplace.local";
        var billingCycle = SubscriptionLifecycleHelper.NormalizeBillingCycle(request.Request.BillingCycle);
        var currency = SubscriptionLifecycleHelper.NormalizeCurrency(request.Request.Currency);
        var isYearly = string.Equals(billingCycle, BillingCycle.Yearly.ToString(), StringComparison.OrdinalIgnoreCase);

        var amountUsd = isYearly ? plan.YearlyPriceUsd : plan.MonthlyPriceUsd;
        var amountTry = isYearly ? plan.YearlyPriceTry : plan.MonthlyPriceTry;
        var exchangeRate = amountUsd > 0m ? Math.Round(amountTry / amountUsd, 4) : 34.5m;
        var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        var gatewayResult = await paymentGateway.CreateCheckoutSessionAsync(
            new PaymentGatewaySessionCreateInput(
                request.UserId,
                username,
                email,
                plan,
                billingCycle,
                amountUsd,
                amountTry,
                currency,
                invoiceNumber,
                request.Request.ReturnBaseUrl,
                request.ClientIp),
            cancellationToken);

        if (!gatewayResult.Success)
        {
            return ServiceResponse.CreateError(
                gatewayResult.ErrorMessage ?? "Stripe Sanal POS oturumu başlatılamadı.",
                HttpStatusCode.BadGateway);
        }

        var tx = new SubscriptionTransaction
        {
            SessionId = gatewayResult.SessionId,
            UserId = request.UserId,
            Username = username,
            PlanId = plan.Id,
            PlanName = plan.Name,
            BillingCycle = billingCycle,
            AmountUsd = amountUsd,
            AmountTry = amountTry,
            Currency = currency,
            ExchangeRate = exchangeRate,
            Status = "Pending",
            PaymentProvider = gatewayResult.Provider,
            InvoiceNumber = invoiceNumber,
            CreatedAtUtc = DateTime.UtcNow
        };

        await subscriptionRepository.AddTransactionAsync(tx, cancellationToken);

        var dto = new CheckoutSessionResponseDto(
            gatewayResult.SessionId,
            gatewayResult.CheckoutUrl,
            gatewayResult.Mode,
            gatewayResult.Provider,
            plan.Id,
            plan.Name,
            billingCycle,
            amountUsd,
            amountTry,
            currency,
            invoiceNumber,
            gatewayResult.ExpiresAtUtc.ToString("O"));

        return ServiceResponse.CreateSuccess("Stripe Sanal POS oturumu oluşturuldu.", dto);
    }
}
