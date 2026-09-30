namespace EtsyMarketPlace.Infrastructure.EtsyIntegration;

public sealed class EtsyIntegrationOptions
{
    public string DatabasePath { get; set; } = string.Empty;

    public string ResolveDatabasePath()
    {
        if (!string.IsNullOrWhiteSpace(DatabasePath))
            return Environment.ExpandEnvironmentVariables(DatabasePath);

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "EtsyMarketPlace",
            "etsy-finance.db");
    }
}
