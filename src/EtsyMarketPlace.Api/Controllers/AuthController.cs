namespace EtsyMarketPlace.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Application.Features.Auth.Commands.Login;
using EtsyMarketPlace.Application.Features.Auth.Commands.Register;
using EtsyMarketPlace.Application.Features.Auth.Queries.GetCurrentUser;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly JwtTokenService _jwtService;

    public AuthController(IMediator mediator, JwtTokenService jwtService)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _jwtService = jwtService ?? throw new ArgumentNullException(nameof(jwtService));
    }

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

    [HttpPost("register")]
    [EndpointSummary("Yeni Kullanıcı Kaydı")]
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

    [HttpGet("me")]
    [EndpointSummary("Geçerli Kullanıcı Bilgileri")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var authHeader = HttpContext.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized();
        }

        var token = authHeader["Bearer ".Length..].Trim();
        var (isValid, _, userId, _, _) = _jwtService.ValidateToken(token);
        if (!isValid || string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var req = new GetCurrentUserRequest { UserId = userId };
        var response = await _mediator.Send(req, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }
}
