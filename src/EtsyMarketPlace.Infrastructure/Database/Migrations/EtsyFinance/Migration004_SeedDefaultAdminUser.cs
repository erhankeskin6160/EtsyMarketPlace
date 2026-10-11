using System.Security.Cryptography;
using EtsyMarketPlace.Application.Auth;
using Microsoft.Data.Sqlite;

namespace EtsyMarketPlace.Infrastructure.Database.Migrations.EtsyFinance;

public sealed class Migration004_SeedDefaultAdminUser : ISchemaMigration
{
    public int Version => 4;
    public string Name => "SeedDefaultAdminUser";

    public async Task UpAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var checkCmd = connection.CreateCommand();
        checkCmd.Transaction = transaction;
        checkCmd.CommandText = "SELECT COUNT(*) FROM app_users;";
        var userCount = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(cancellationToken) ?? 0);
        if (userCount > 0)
        {
            return;
        }

        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var salt = Convert.ToBase64String(saltBytes);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2("Admin123*!", saltBytes, 100_000, HashAlgorithmName.SHA256, 32);
        var hash = Convert.ToBase64String(hashBytes);

        await using var seedCmd = connection.CreateCommand();
        seedCmd.Transaction = transaction;
        seedCmd.CommandText = """
            INSERT INTO app_users (id, username, email, password_hash, password_salt, role, assigned_shop_ids, monthly_ai_token_quota, used_ai_tokens, is_active, created_at)
            VALUES ($id, $username, $email, $hash, $salt, $role, $shops, $quota, 0, 1, $createdAt);
            """;
        seedCmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        seedCmd.Parameters.AddWithValue("$username", "admin");
        seedCmd.Parameters.AddWithValue("$email", "admin@etsymarketplace.local");
        seedCmd.Parameters.AddWithValue("$hash", hash);
        seedCmd.Parameters.AddWithValue("$salt", salt);
        seedCmd.Parameters.AddWithValue("$role", UserRoles.Admin);
        seedCmd.Parameters.AddWithValue("$shops", "[\"53236321\"]");
        seedCmd.Parameters.AddWithValue("$quota", 2000000);
        seedCmd.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        await seedCmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
