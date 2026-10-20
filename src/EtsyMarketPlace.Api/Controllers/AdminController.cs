namespace EtsyMarketPlace.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Features.Admin.Commands.ToggleUserStatus;
using EtsyMarketPlace.Application.Features.Admin.Commands.UpdateUser;
using EtsyMarketPlace.Application.Features.Admin.Queries.GetAllUsers;
using EtsyMarketPlace.Application.Features.Admin.Queries.GetAuditLogs;
using EtsyMarketPlace.Application.Features.Admin.Queries.GetSystemStats;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly JwtTokenService _jwtService;

    public AdminController(IMediator mediator, JwtTokenService jwtService)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _jwtService = jwtService ?? throw new ArgumentNullException(nameof(jwtService));
    }

    private (bool IsValid, string? AdminId, string? AdminName) ValidateAdmin()
    {
        var authHeader = HttpContext.Request.Headers["Authorization"].ToString();
        var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authHeader["Bearer ".Length..].Trim() : null;
        var (isValid, _, adminId, role, adminName) = _jwtService.ValidateToken(token);
        if (!isValid || role != UserRoles.Admin)
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
}
