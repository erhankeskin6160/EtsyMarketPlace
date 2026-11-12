using System.IO;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
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
    private static readonly (string InformationalVersion, string CommitSha) VersionInfo = ResolveVersionInfo();

    public SystemController(IConfigurationStatusService configStatusService)
    {
        _configStatusService = configStatusService;
    }

    private static (string InformationalVersion, string CommitSha) ResolveVersionInfo()
    {
        var infoVer = typeof(SystemController).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "2.4.0";

        var commitSha = Environment.GetEnvironmentVariable("GIT_COMMIT_SHA")
            ?? Environment.GetEnvironmentVariable("GITHUB_SHA");

        if (string.IsNullOrWhiteSpace(commitSha) && infoVer.Contains('+'))
        {
            var parts = infoVer.Split('+');
            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                commitSha = parts[1].Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(commitSha))
        {
            commitSha = "dev-build";
        }

        return (infoVer, commitSha);
    }

    [HttpGet("")]
    [EndpointSummary("API Kök Bilgilendirme / Web Kokpiti")]
    public IActionResult GetRootInfo()
    {
        var acceptHeader = Request.Headers.Accept.ToString();
        if (acceptHeader.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
            if (System.IO.File.Exists(webRoot))
            {
                Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                Response.Headers.Pragma = "no-cache";
                Response.Headers.Expires = "0";
                return PhysicalFile(webRoot, "text/html");
            }
        }

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
            informationalVersion = VersionInfo.InformationalVersion,
            commitSha = VersionInfo.CommitSha,
            service = "EtsyMarketPlace VDS Command Engine",
            status = "Online",
            database = "PostgreSQL",
            uptimeSeconds = Environment.TickCount64 / 1000,
            serverTime = DateTimeOffset.UtcNow.ToString("O"),
            runtime = new
            {
                framework = RuntimeInformation.FrameworkDescription,
                clrVersion = Environment.Version.ToString(),
                os = RuntimeInformation.OSDescription,
                architecture = RuntimeInformation.OSArchitecture.ToString()
            }
        });
    }
}
