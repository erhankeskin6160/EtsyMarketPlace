namespace EtsyMarketPlace.Infrastructure.Database.Migrations;

/// <summary>
/// Audit record of an applied database migration stored in __schema_migrations.
/// </summary>
public sealed record MigrationAppliedRecord(
    int Version,
    string Name,
    DateTimeOffset AppliedAtUtc,
    long ExecutionTimeMs);
