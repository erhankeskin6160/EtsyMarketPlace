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
using EtsyMarketPlace.Application.Features.Auth.Commands.Login;
using EtsyMarketPlace.Application.Features.Auth.Commands.Logout;
using EtsyMarketPlace.Application.Features.Auth.Commands.Register;
using EtsyMarketPlace.Application.Features.Auth.Queries.GetCurrentUser;

[ApiController]
[Route("api/v{version:apiVersion}/auth")]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [EndpointSummary("Kullanıcı Girişi")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var req = new LoginUserRequest
        {
            UsernameOrEmail = request?.UsernameOrEmail ?? string.Empty,
            Password = request?.Password ?? string.Empty,
            RemoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        var response = await _mediator.Send(req, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [Authorize(Roles = UserRoles.AdminRoles)]
    [HttpPost("register")]
    [EndpointSummary("Yeni Kullanıcı Kaydı (yalnız yönetici)")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var req = new RegisterUserRequest
        {
            Username = request?.Username ?? string.Empty,
            Email = request?.Email ?? string.Empty,
            Password = request?.Password ?? string.Empty,
            ShopId = request?.ShopId,
            RemoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        var response = await _mediator.Send(req, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [Authorize]
    [HttpPost("logout")]
    [EndpointSummary("Oturumu Kapat (SecurityStamp İptali)")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var req = new LogoutUserRequest
        {
            UserId = userId,
            Username = User.Identity?.Name ?? userId,
            RemoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        var response = await _mediator.Send(req, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [Authorize]
    [HttpGet("me")]
    [EndpointSummary("Geçerli Kullanıcı Bilgileri")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var req = new GetCurrentUserRequest { UserId = userId };
        var response = await _mediator.Send(req, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }
}
