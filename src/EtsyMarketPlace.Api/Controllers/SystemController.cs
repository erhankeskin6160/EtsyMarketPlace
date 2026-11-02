using System;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EtsyMarketPlace.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("")]
public class SystemController : ControllerBase
{
    private readonly IConfigurationStatusService _configStatusService;

    public SystemController(IConfigurationStatusService configStatusService)
    {
        _configStatusService = configStatusService;
    }

    [HttpGet("")]
    [EndpointSummary("API Kök Bilgilendirme")]
    public IActionResult GetRootInfo()
    {
        return Ok(new
        {
            status = "Healthy",
            service = "EtsyMarketPlace VDS API",
            version = "1.0.0",
            timestamp = DateTimeOffset.UtcNow,
            endpoints = new
            {
                swagger = "/swagger",
                health = "/health",
                configStatus = "/api/v1/system/config-status",
                auth = "/api/auth/login",
                mcp = "/mcp"
            }
        });
    }

    [HttpGet("health")]
    [EndpointSummary("API ve Sunucu Sağlık Kontrolü")]
    public async Task<IActionResult> HealthCheck(CancellationToken ct)
    {
        var report = await _configStatusService.GetStatusAsync(ct);
        if (report.Status == "Unhealthy")
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "Unhealthy",
                missingRequired = report.MissingRequired,
                timestamp = report.Timestamp
            });
        }

        return Ok(new
        {
            status = report.Status, // Healthy veya Degraded
            missingOptional = report.MissingOptional,
            timestamp = report.Timestamp
        });
    }

    [HttpGet("api/system/config-status")]
    [HttpGet("api/v1/system/config-status")]
    [EndpointSummary("Sistem ve Entegrasyon Yapılandırma Durum Raporu")]
    public async Task<IActionResult> GetConfigStatus(CancellationToken ct)
    {
        var report = await _configStatusService.GetStatusAsync(ct);
        return Ok(report);
    }

    [HttpGet("api/system/version")]
    [HttpGet("api/v1/system/version")]
    [EndpointSummary("VDS API Sürüm & Sağlık Durumu")]
    public IActionResult GetSystemVersion()
    {
        return Ok(new
        {
            version = "2.4.0",
            service = "EtsyMarketPlace VDS Command Engine",
            status = "Online",
            database = "PostgreSQL",
            uptimeSeconds = Environment.TickCount64 / 1000,
            serverTime = DateTimeOffset.UtcNow.ToString("O")
        });
    }
}
