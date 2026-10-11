namespace EtsyMarketPlace.Infrastructure.Database.Migrations.EtsyFinance;

public sealed class Migration002_CreateIdentityAndAuditTables : SqlMigration
{
    public override int Version => 2;
    public override string Name => "CreateIdentityAndAuditTables";

    public override string UpSql => """
        CREATE TABLE IF NOT EXISTS app_users (
            id TEXT PRIMARY KEY,
            username TEXT NOT NULL UNIQUE,
            email TEXT NOT NULL UNIQUE,
            password_hash TEXT NOT NULL,
            password_salt TEXT NOT NULL,
            role TEXT NOT NULL DEFAULT 'StoreOwner',
            assigned_shop_ids TEXT NOT NULL DEFAULT '[]',
            monthly_ai_token_quota INTEGER NOT NULL DEFAULT 500000,
            used_ai_tokens INTEGER NOT NULL DEFAULT 0,
            is_active INTEGER NOT NULL DEFAULT 1,
            created_at TEXT NOT NULL,
            last_login_at TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS audit_logs (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id TEXT NOT NULL,
            username TEXT NOT NULL,
            action TEXT NOT NULL,
            details TEXT NULL,
            ip_address TEXT NULL,
            timestamp TEXT NOT NULL
        );
        """;
}
