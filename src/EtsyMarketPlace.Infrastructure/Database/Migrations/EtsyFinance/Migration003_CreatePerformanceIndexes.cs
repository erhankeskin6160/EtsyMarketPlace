namespace EtsyMarketPlace.Infrastructure.Database.Migrations.EtsyFinance;

public sealed class Migration003_CreatePerformanceIndexes : SqlMigration
{
    public override int Version => 3;
    public override string Name => "CreatePerformanceIndexes";

    public override string UpSql => """
        CREATE INDEX IF NOT EXISTS ix_listing_ai_audits_shop ON listing_ai_audits(shop_id);

        CREATE INDEX IF NOT EXISTS ix_bank_payouts_shop_date ON bank_payouts(shop_id, occurred_at);
        CREATE INDEX IF NOT EXISTS ix_financial_transactions_shop_date ON financial_transactions(shop_id, occurred_at);
        CREATE INDEX IF NOT EXISTS ix_order_costs_shop_date ON order_costs(shop_id, created_at);
        CREATE INDEX IF NOT EXISTS ix_listing_traffic_shop_date ON listing_traffic_daily(shop_id, snapshot_date);
        CREATE INDEX IF NOT EXISTS ix_monthly_orders_shop ON monthly_order_summaries(shop_id, year_month);

        CREATE INDEX IF NOT EXISTS ix_app_users_username ON app_users(username);
        CREATE INDEX IF NOT EXISTS ix_app_users_email ON app_users(email);
        CREATE INDEX IF NOT EXISTS ix_audit_logs_timestamp ON audit_logs(timestamp);
        """;
}
