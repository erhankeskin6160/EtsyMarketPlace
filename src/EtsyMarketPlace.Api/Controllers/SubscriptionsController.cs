using System;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Features.Subscriptions.Commands.ConfirmAdminTestPayment;
using EtsyMarketPlace.Application.Features.Subscriptions.Commands.ConfirmCheckoutPayment;
using EtsyMarketPlace.Application.Features.Subscriptions.Commands.CreateCheckoutSession;
using EtsyMarketPlace.Application.Features.Subscriptions.Commands.ProcessPaymentCallback;
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
    [EndpointSummary("İyzico / PayTR / Stripe Abonelik Ödeme Oturumu Oluştur")]
    public async Task<IActionResult> CreateCheckoutSession(
        [FromBody] CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var (userId, role, username, email) = ExtractUserClaims();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var cmd = new CreateCheckoutSessionCommand(userId, role, username, email, request, clientIp);
        var response = await _mediator.Send(cmd, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("confirm-payment")]
    [EndpointSummary("Sanal POS Ödeme Onayı & Paket Aktivasyonu")]
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

    [Authorize(Roles = UserRoles.Admin)]
    [HttpPost("confirm-admin-test")]
    [EndpointSummary("Yönetici Test Onayı ile Abonelik Aktivasyonu")]
    public async Task<IActionResult> ConfirmAdminTest(
        [FromBody] AdminTestConfirmRequest request,
        CancellationToken cancellationToken = default)
    {
        var (userId, role, username, _) = ExtractUserClaims();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var cmd = new ConfirmAdminTestPaymentCommand(request.SessionId, userId, role, username, ip);
        var response = await _mediator.Send(cmd, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [AllowAnonymous]
    [HttpPost("callback/paytr")]
    [EndpointSummary("PayTR Bildirim (Callback / IPN) Uç Noktası")]
    public async Task<IActionResult> PaytrCallback(CancellationToken cancellationToken = default)
    {
        var form = Request.HasFormContentType
            ? await Request.ReadFormAsync(cancellationToken)
            : null;

        var input = new PaytrCallbackInput(
            MerchantOid: form?["merchant_oid"].ToString() ?? string.Empty,
            Status: form?["status"].ToString() ?? string.Empty,
            TotalAmount: form?["total_amount"].ToString() ?? string.Empty,
            Hash: form?["hash"].ToString() ?? string.Empty,
            FailedReasonMsg: form?["failed_reason_msg"].ToString(),
            PaymentType: form?["payment_type"].ToString(),
            Currency: form?["currency"].ToString());

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var response = await _mediator.Send(new ProcessPaymentCallbackCommand("PayTR", input, null, ip), cancellationToken);
        if (!response.Success)
            return StatusCode(response.StatusCode, response.Message);

        return Content("OK", "text/plain");
    }

    [AllowAnonymous]
    [HttpPost("callback/iyzico")]
    [EndpointSummary("İyzico CheckoutForm 3D Secure Callback Uç Noktası")]
    public async Task<IActionResult> IyzicoCallback(
        [FromQuery] string? sessionId,
        CancellationToken cancellationToken = default)
    {
        var form = Request.HasFormContentType
            ? await Request.ReadFormAsync(cancellationToken)
            : null;

        var token = form?["token"].ToString();
        if (string.IsNullOrWhiteSpace(token))
            token = Request.Query["token"].ToString();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var response = await _mediator.Send(
            new ProcessPaymentCallbackCommand(
                "Iyzico",
                null,
                new IyzicoCallbackInput(sessionId ?? string.Empty, token ?? string.Empty),
                ip),
            cancellationToken);

        var result = response.Data as PaymentCallbackProcessResultDto;
        var paymentStatus = result?.IsPaid == true ? "success" : "failed";
        var safeSession = Uri.EscapeDataString(sessionId ?? result?.SessionId ?? string.Empty);
        var redirectUrl = $"/settings/subscription?payment_status={paymentStatus}&provider=Iyzico&sessionId={safeSession}";

        var jsonUrl = JsonSerializer.Serialize(redirectUrl);
        var htmlUrl = WebUtility.HtmlEncode(redirectUrl);
        var html = $"<!doctype html><html><head><meta charset=\"utf-8\"><meta http-equiv=\"refresh\" content=\"0;url={htmlUrl}\"><script>var u={jsonUrl};try{{(window.top||window).location.href=u;}}catch(e){{window.location.href=u;}}</script></head><body>Ödeme sonucu yönlendiriliyor...</body></html>";
        return Content(html, "text/html");
    }

    public sealed record AdminTestConfirmRequest(string SessionId);

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
