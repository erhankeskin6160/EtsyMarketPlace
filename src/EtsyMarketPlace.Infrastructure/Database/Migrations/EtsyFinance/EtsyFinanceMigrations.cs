namespace EtsyMarketPlace.Infrastructure.Database.Migrations.EtsyFinance;

/// <summary>
/// Registry of all ordered schema migrations for the Etsy Finance database.
/// </summary>
public static class EtsyFinanceMigrations
{
    public static IReadOnlyList<ISchemaMigration> All { get; } = new ISchemaMigration[]
    {
        new Migration001_CreateCoreIntegrationTables(),
        new Migration002_CreateIdentityAndAuditTables(),
        new Migration003_CreatePerformanceIndexes(),
        new Migration004_SeedDefaultAdminUser()
    };
}
