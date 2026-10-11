using EtsyMarketPlace.Infrastructure.Database.Migrations;
using EtsyMarketPlace.Infrastructure.Database.Migrations.EtsyFinance;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EtsyMarketPlace.Api.Tests;

public sealed class SqliteMigrationRunnerTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _connectionString;

    public SqliteMigrationRunnerTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_migration_{Guid.NewGuid():N}.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _tempDbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempDbPath))
            {
                File.Delete(_tempDbPath);
            }
        }
        catch
        {
            // Ignore cleanup errors on temp files
        }
    }

    [Fact]
    public async Task MigrateAsync_AppliesAllMigrationsInOrder()
    {
        var runner = new SqliteMigrationRunner();
        var m1 = new TestScriptMigration(1, "CreateUsers", "CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT);");
        var m2 = new TestScriptMigration(2, "AddEmail", "ALTER TABLE users ADD COLUMN email TEXT;");

        var applied = await runner.MigrateAsync(_connectionString, new[] { m2, m1 });

        Assert.Equal(2, applied.Count);
        Assert.Equal(1, applied[0].Version);
        Assert.Equal("CreateUsers", applied[0].Name);
        Assert.Equal(2, applied[1].Version);
        Assert.Equal("AddEmail", applied[1].Name);

        var history = await runner.GetAppliedMigrationsAsync(_connectionString);
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public async Task MigrateAsync_IsIdempotent_SecondRunAppliesZeroMigrations()
    {
        var runner = new SqliteMigrationRunner();
        var m1 = new TestScriptMigration(1, "CreateOrders", "CREATE TABLE orders (id INTEGER PRIMARY KEY);");

        var firstRun = await runner.MigrateAsync(_connectionString, new[] { m1 });
        Assert.Single(firstRun);

        var secondRun = await runner.MigrateAsync(_connectionString, new[] { m1 });
        Assert.Empty(secondRun);
    }

    [Fact]
    public async Task MigrateAsync_WithDuplicateVersions_ThrowsInvalidOperationException()
    {
        var runner = new SqliteMigrationRunner();
        var m1 = new TestScriptMigration(1, "First", "CREATE TABLE t1 (id INT);");
        var m2 = new TestScriptMigration(1, "Second", "CREATE TABLE t2 (id INT);");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.MigrateAsync(_connectionString, new[] { m1, m2 }));
    }

    [Fact]
    public async Task MigrateAsync_WhenMigrationFails_RollsBackTransactionAndDoesNotRecordVersion()
    {
        var runner = new SqliteMigrationRunner();
        var m1 = new TestScriptMigration(1, "Valid", "CREATE TABLE t1 (id INT);");
        var m2 = new TestScriptMigration(2, "Invalid", "INVALID SQL SYNTAX HERE;");

        await runner.MigrateAsync(_connectionString, new[] { m1 });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.MigrateAsync(_connectionString, new[] { m1, m2 }));

        var applied = await runner.GetAppliedMigrationsAsync(_connectionString);
        Assert.Single(applied);
        Assert.Equal(1, applied[0].Version);
    }

    [Fact]
    public async Task EtsyFinanceMigrations_CanRunAgainstCleanDatabase()
    {
        var runner = new SqliteMigrationRunner();
        var applied = await runner.MigrateAsync(_connectionString, EtsyFinanceMigrations.All);

        Assert.Equal(EtsyFinanceMigrations.All.Count, applied.Count);

        // Verify tables exist and default admin is seeded
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM app_users WHERE username = 'admin';";
        var adminCount = Convert.ToInt64(await cmd.ExecuteScalarAsync() ?? 0);
        Assert.Equal(1, adminCount);

        // Second run must be completely idempotent
        var secondRun = await runner.MigrateAsync(_connectionString, EtsyFinanceMigrations.All);
        Assert.Empty(secondRun);
    }

    private sealed class TestScriptMigration(int version, string name, string sql) : SqlMigration
    {
        public override int Version => version;
        public override string Name => name;
        public override string UpSql => sql;
    }
}
