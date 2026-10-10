using Microsoft.AspNetCore.Mvc;

namespace EtsyMarketPlace.Api.Controllers;

[ApiController]
[Route("")]
public class SystemController : ControllerBase
{
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
                auth = "/api/auth/login",
                mcp = "/mcp"
            }
        });
    }

    [HttpGet("health")]
    [EndpointSummary("API ve Sunucu Sağlık Kontrolü")]
    public IActionResult HealthCheck()
    {
        return Ok(new { status = "Healthy", timestamp = DateTime.UtcNow });
    }

    [HttpGet("api/system/version")]
    [EndpointSummary("VDS API Sürüm & Sağlık Durumu")]
    public IActionResult GetSystemVersion()
    {
        return Ok(new
        {
            version = "2.4.0",
            service = "EtsyMarketPlace VDS Command Engine",
            status = "Online",
            database = "SQLite-WAL Encrypted",
            uptimeSeconds = Environment.TickCount64 / 1000,
            serverTime = DateTimeOffset.UtcNow.ToString("O")
        });
    }
}
