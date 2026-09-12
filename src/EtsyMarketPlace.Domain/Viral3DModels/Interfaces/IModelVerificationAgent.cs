namespace EtsyMarketPlace.Domain.Viral3DModels.Interfaces;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Viral3DModels.Entities;
using EtsyMarketPlace.Domain.Viral3DModels.Enums;

public sealed class VerifiedModelResult
{
    public bool IsVerified { get; set; }
    public string OriginalUrl { get; set; } = string.Empty;
    public string VerifiedUrl { get; set; } = string.Empty;
    public string? VerifiedImageUrl { get; set; }
    public string? VerifiedAuthor { get; set; }
    public string? VerifiedTitle { get; set; }
    public bool HasIpCopyrightRisk { get; set; }
    public string? IpRiskWarning { get; set; }
    public string AgentDiagnosticNotes { get; set; } = string.Empty;
}

/// <summary>
/// Autonomous AI Agent responsible for live verification of 3D model links,
/// anti-drift self-healing, genuine image CDN resolution, and Etsy IP copyright protection.
/// </summary>
public interface IModelVerificationAgent
{
    Task<VerifiedModelResult> VerifyAndHealModelAsync(Trending3DModel model, CancellationToken ct = default);
    Task<VerifiedModelResult> VerifyWithVisualBrowserAsync(Trending3DModel model, System.Action<string>? statusCallback = null, CancellationToken ct = default);
    Task<IReadOnlyList<Trending3DModel>> ScoutTrendingModelsAsync(string query, ModelPlatformType? platform = null, CancellationToken ct = default);
    Task<IReadOnlyList<Trending3DModel>> ScoutAndHarvestForShopAsync(
        EtsyMarketPlace.Domain.Viral3DModels.ValueObjects.ShopNicheProfile shopProfile,
        ModelPlatformType platform = ModelPlatformType.Printables,
        int maxModels = 20,
        System.Action<string>? statusCallback = null,
        CancellationToken ct = default);
}
