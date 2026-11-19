namespace EtsyMarketPlace.Api.Controllers;

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Features.Admin.Commands.ToggleUserStatus;
using EtsyMarketPlace.Application.Features.Admin.Commands.UpdateUser;
using EtsyMarketPlace.Application.Features.Admin.Queries.GetAllUsers;
using EtsyMarketPlace.Application.Features.Admin.Queries.GetAuditLogs;
using EtsyMarketPlace.Application.Features.Admin.Queries.GetSystemStats;
using EtsyMarketPlace.Application.Features.Subscriptions.Commands.AdminAssignSubscription;
using EtsyMarketPlace.Application.Features.Subscriptions.Commands.AdminUpdatePaymentGatewaySettings;
using EtsyMarketPlace.Application.Features.Subscriptions.Commands.AdminUpdateSubscriptionPlan;
using EtsyMarketPlace.Application.Features.Subscriptions.Queries.GetPaymentGatewaySettings;
using EtsyMarketPlace.Application.Subscriptions;

[Authorize(Roles = EtsyMarketPlace.Application.Auth.UserRoles.AdminRoles)]
[ApiController]
[Route("api/v{version:apiVersion}/admin")]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    private (bool IsValid, string? AdminId, string? AdminName) ValidateAdmin()
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var adminName = User.Identity?.Name;
        var isAdmin = User.IsInRole(UserRoles.Admin) || User.IsInRole(UserRoles.SuperAdmin);
        if (!isAdmin || string.IsNullOrWhiteSpace(adminId))
        {
            return (false, null, null);
        }
        return (true, adminId, adminName);
    }

    [HttpGet("users")]
    [EndpointSummary("Kullanıcı Listesi")]
    public async Task<IActionResult> GetAllUsers(CancellationToken cancellationToken)
    {
        var (isAdmin, _, _) = ValidateAdmin();
        if (!isAdmin) return StatusCode(StatusCodes.Status403Forbidden);

        var request = new GetAllUsersRequest();
        var response = await _mediator.Send(request, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPut("users/{id}")]
    [EndpointSummary("Kullanıcı Düzenleme & Yetki/Mağaza Atama")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var (isAdmin, adminId, adminName) = ValidateAdmin();
        if (!isAdmin) return StatusCode(StatusCodes.Status403Forbidden);

        var cmd = new UpdateUserCommandRequest
        {
            Id = id,
            Request = request,
            AdminId = adminId,
            AdminName = adminName,
            RemoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        var response = await _mediator.Send(cmd, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("users/{id}/toggle-status")]
    [EndpointSummary("Kullanıcı Durumu Değiştirme (Aktif/Askıda)")]
    public async Task<IActionResult> ToggleUserStatus(string id, CancellationToken cancellationToken)
    {
        var (isAdmin, adminId, adminName) = ValidateAdmin();
        if (!isAdmin) return StatusCode(StatusCodes.Status403Forbidden);

        var cmd = new ToggleUserStatusRequest
        {
            Id = id,
            AdminId = adminId,
            AdminName = adminName,
            RemoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        var response = await _mediator.Send(cmd, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("audit-logs")]
    [EndpointSummary("Audit Güvenlik Günlüğü")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] int limit = 100, CancellationToken cancellationToken = default)
    {
        var (isAdmin, _, _) = ValidateAdmin();
        if (!isAdmin) return StatusCode(StatusCodes.Status403Forbidden);

        var request = new GetAuditLogsRequest { Limit = limit };
        var response = await _mediator.Send(request, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("system-stats")]
    [EndpointSummary("Sistem ve Kullanıcı İstatistikleri")]
    public async Task<IActionResult> GetSystemStats(CancellationToken cancellationToken)
    {
        var (isAdmin, _, _) = ValidateAdmin();
        if (!isAdmin) return StatusCode(StatusCodes.Status403Forbidden);

        var request = new GetSystemStatsRequest();
        var response = await _mediator.Send(request, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("users/{id}/subscription")]
    [EndpointSummary("Kullanıcıya Abonelik Paketi ve Kota Ata")]
    public async Task<IActionResult> AssignUserSubscription(
        string id,
        [FromBody] AdminAssignSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var (isAdmin, adminId, adminName) = ValidateAdmin();
        if (!isAdmin) return StatusCode(StatusCodes.Status403Forbidden);

        var cmd = new AdminAssignSubscriptionCommand(
            id,
            adminId,
            adminName,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            request);

        var response = await _mediator.Send(cmd, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPut("subscriptions/plans/{planId}")]
    [EndpointSummary("Abonelik Paketi Fiyat ve Limitlerini Güncelle")]
    public async Task<IActionResult> UpdateSubscriptionPlan(
        string planId,
        [FromBody] AdminUpdateSubscriptionPlanRequest request,
        CancellationToken cancellationToken)
    {
        var (isAdmin, adminId, adminName) = ValidateAdmin();
        if (!isAdmin) return StatusCode(StatusCodes.Status403Forbidden);

        var cmd = new AdminUpdateSubscriptionPlanCommand(
            planId,
            adminId,
            adminName,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            request);

        var response = await _mediator.Send(cmd, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("subscriptions/payment-settings")]
    [EndpointSummary("Ödeme Altyapısı (İyzico / PayTR / Stripe) Ayarlarını Getir")]
    public async Task<IActionResult> GetPaymentGatewaySettings(CancellationToken cancellationToken)
    {
        var (isAdmin, _, _) = ValidateAdmin();
        if (!isAdmin) return StatusCode(StatusCodes.Status403Forbidden);

        var response = await _mediator.Send(new GetPaymentGatewaySettingsQuery(), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPut("subscriptions/payment-settings")]
    [EndpointSummary("Ödeme Altyapısı (İyzico / PayTR / Stripe) Ayarlarını Güncelle")]
    public async Task<IActionResult> UpdatePaymentGatewaySettings(
        [FromBody] UpdatePaymentGatewaySettingsRequest request,
        CancellationToken cancellationToken)
    {
        var (isAdmin, adminId, adminName) = ValidateAdmin();
        if (!isAdmin) return StatusCode(StatusCodes.Status403Forbidden);

        var cmd = new AdminUpdatePaymentGatewaySettingsCommand(
            adminId,
            adminName,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            request);

        var response = await _mediator.Send(cmd, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }
}
