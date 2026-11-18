using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Features.Subscriptions.Commands.ConfirmCheckoutPayment;
using EtsyMarketPlace.Application.Features.Subscriptions.Commands.CreateCheckoutSession;
using EtsyMarketPlace.Application.Features.Subscriptions.Queries.CheckSubscriptionFeature;
using EtsyMarketPlace.Application.Features.Subscriptions.Queries.GetMySubscription;
using EtsyMarketPlace.Application.Features.Subscriptions.Queries.GetSubscriptionPlans;
using EtsyMarketPlace.Application.Features.Subscriptions.Queries.GetSubscriptionTransactions;
using EtsyMarketPlace.Application.Subscriptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EtsyMarketPlace.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/subscriptions")]
[Route("api/subscriptions")]
public class SubscriptionsController : BaseApiController
{
    private readonly IMediator _mediator;

    public SubscriptionsController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [AllowAnonymous]
    [HttpGet("plans")]
    [EndpointSummary("SaaS Abonelik Paketleri Listesi")]
    public async Task<IActionResult> GetPlans([FromQuery] bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var response = await _mediator.Send(new GetSubscriptionPlansQuery(activeOnly), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("me")]
    [EndpointSummary("Aktif Kullanıcının Abonelik & Kota Özeti")]
    public async Task<IActionResult> GetMySubscription(CancellationToken cancellationToken = default)
    {
        var (userId, role, username, _) = ExtractUserClaims();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var response = await _mediator.Send(new GetMySubscriptionQuery(userId, role, username), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("checkout-session")]
    [EndpointSummary("Stripe / Sanal POS Abonelik Ödeme Oturumu Oluştur")]
    public async Task<IActionResult> CreateCheckoutSession(
        [FromBody] CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var (userId, role, username, email) = ExtractUserClaims();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var cmd = new CreateCheckoutSessionCommand(userId, role, username, email, request);
        var response = await _mediator.Send(cmd, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("confirm-payment")]
    [EndpointSummary("Stripe Sanal POS Ödeme Onayı & Paket Aktivasyonu")]
    public async Task<IActionResult> ConfirmPayment(
        [FromBody] ConfirmVirtualPosPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var (userId, role, username, _) = ExtractUserClaims();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var cmd = new ConfirmCheckoutPaymentCommand(userId, role, username, ip, request);
        var response = await _mediator.Send(cmd, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("transactions")]
    [EndpointSummary("Abonelik Ödeme ve Fatura Geçmişi")]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var (userId, _, _, _) = ExtractUserClaims();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var response = await _mediator.Send(new GetSubscriptionTransactionsQuery(userId, limit), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("check-feature/{featureKey}")]
    [EndpointSummary("Modül / Özellik Abonelik Yetki Kontrolü")]
    public async Task<IActionResult> CheckFeature(string featureKey, CancellationToken cancellationToken = default)
    {
        var (userId, role, _, _) = ExtractUserClaims();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var response = await _mediator.Send(new CheckSubscriptionFeatureQuery(userId, featureKey, role), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    private (string? UserId, string? Role, string? Username, string? Email) ExtractUserClaims()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = User.FindFirstValue(ClaimTypes.Role);
        var username = User.FindFirstValue(ClaimTypes.Name);
        var email = User.FindFirstValue(ClaimTypes.Email);
        return (userId, role, username, email);
    }
}
