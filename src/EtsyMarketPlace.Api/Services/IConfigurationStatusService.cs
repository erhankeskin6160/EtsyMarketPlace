namespace EtsyMarketPlace.Api.Services;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class ConfigStatusResult
{
    public string Status { get; set; } = "Healthy"; // Healthy, Degraded, Unhealthy
    public string Service { get; set; } = "EtsyMarketPlace VDS Command Engine";
    public string Version { get; set; } = "2.5.0";
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, ConfigComponentStatus> Components { get; set; } = new();
    public List<string> MissingRequired { get; set; } = new();
    public List<string> MissingOptional { get; set; } = new();
}

public sealed class ConfigComponentStatus
{
    public bool IsConfigured { get; set; }
    public string Status { get; set; } = "Healthy"; // Healthy, Degraded, Optional, Unhealthy
    public string Summary { get; set; } = string.Empty;
    public string? Action { get; set; }
}

public interface IConfigurationStatusService
{
    Task<ConfigStatusResult> GetStatusAsync(CancellationToken cancellationToken = default);
}
