namespace EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;

public record ModelLicenseInfo
{
    public string LicenseName { get; init; } = "Standard Digital File";
    public bool IsCommercialAllowed { get; init; } = false;
    public bool RequiresAttribution { get; init; } = true;
    public string LicenseCode { get; init; } = "CC-BY-NC";
    public string Notes { get; init; } = string.Empty;

    public static ModelLicenseInfo Commercial(string name = "Ticari Satış Serbest", bool attribution = false) =>
        new() { LicenseName = name, IsCommercialAllowed = true, RequiresAttribution = attribution, LicenseCode = "COMMERCIAL" };

    public static ModelLicenseInfo CreativeCommonsCommercial(string code = "CC-BY") =>
        new() { LicenseName = $"Creative Commons ({code}) - Ticari Serbest", IsCommercialAllowed = true, RequiresAttribution = true, LicenseCode = code };

    public static ModelLicenseInfo NonCommercial(string name = "Yalnızca Kişisel Kullanım (Ticari Yasak)") =>
        new() { LicenseName = name, IsCommercialAllowed = false, RequiresAttribution = true, LicenseCode = "NON-COMMERCIAL" };
}
