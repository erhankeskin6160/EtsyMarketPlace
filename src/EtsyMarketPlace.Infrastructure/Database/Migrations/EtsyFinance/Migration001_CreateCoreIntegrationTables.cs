namespace EtsyMarketPlace.Infrastructure.Database.Migrations.EtsyFinance;

public sealed class Migration001_CreateCoreIntegrationTables : SqlMigration
{
    public override int Version => 1;
    public override string Name => "CreateCoreIntegrationTables";

    public override string UpSql => """
        CREATE TABLE IF NOT EXISTS shop_connections (
            shop_id TEXT PRIMARY KEY,
            shop_name TEXT NOT NULL DEFAULT '',
            user_id TEXT NOT NULL DEFAULT '',
            last_synchronized_at TEXT NULL,
            is_active INTEGER NOT NULL DEFAULT 1
        );

        CREATE TABLE IF NOT EXISTS oauth_tokens (
            shop_id TEXT PRIMARY KEY,
            access_token TEXT NOT NULL,
            refresh_token TEXT NOT NULL,
            access_token_expires_at TEXT NOT NULL,
            token_type TEXT NOT NULL DEFAULT 'Bearer'
        );

        CREATE TABLE IF NOT EXISTS sync_states (
            shop_id TEXT NOT NULL,
            data_type TEXT NOT NULL,
            last_successful_sync_at TEXT NULL,
            last_cursor_at TEXT NULL,
            last_error TEXT NULL,
            PRIMARY KEY(shop_id, data_type)
        );

        CREATE TABLE IF NOT EXISTS bank_payouts (
            shop_id TEXT NOT NULL,
            reference_id TEXT NOT NULL,
            occurred_at TEXT NOT NULL,
            amount REAL NOT NULL,
            currency TEXT NOT NULL,
            exchange_rate_to_try REAL NULL,
            status TEXT NOT NULL,
            description TEXT NOT NULL,
            PRIMARY KEY(shop_id, reference_id)
        );

        CREATE TABLE IF NOT EXISTS financial_transactions (
            shop_id TEXT NOT NULL,
            reference_id TEXT NOT NULL,
            occurred_at TEXT NOT NULL,
            gross_sales REAL NOT NULL DEFAULT 0,
            platform_fees REAL NOT NULL DEFAULT 0,
            internal_ads_cost REAL NOT NULL DEFAULT 0,
            external_ads_cost REAL NOT NULL DEFAULT 0,
            product_cost REAL NOT NULL DEFAULT 0,
            shipping_cost REAL NOT NULL DEFAULT 0,
            refunds REAL NOT NULL DEFAULT 0,
            currency TEXT NOT NULL,
            PRIMARY KEY(shop_id, reference_id)
        );

        CREATE TABLE IF NOT EXISTS order_costs (
            shop_id TEXT NOT NULL,
            order_id TEXT NOT NULL,
            created_at TEXT NOT NULL,
            currency TEXT NOT NULL,
            order_total REAL NOT NULL DEFAULT 0,
            product_cost REAL NULL,
            shipping_cost REAL NULL,
            alert_reason TEXT NULL,
            PRIMARY KEY(shop_id, order_id)
        );

        CREATE TABLE IF NOT EXISTS monthly_order_summaries (
            shop_id TEXT NOT NULL,
            year_month TEXT NOT NULL,
            order_count INTEGER NOT NULL DEFAULT 0,
            units_sold INTEGER NOT NULL DEFAULT 0,
            gross_revenue REAL NOT NULL DEFAULT 0,
            avg_order_value REAL NOT NULL DEFAULT 0,
            shipped_orders INTEGER NOT NULL DEFAULT 0,
            unfulfilled_orders INTEGER NOT NULL DEFAULT 0,
            currency TEXT NOT NULL DEFAULT 'USD',
            PRIMARY KEY(shop_id, year_month)
        );

        CREATE TABLE IF NOT EXISTS listing_traffic_daily (
            shop_id TEXT NOT NULL,
            listing_id INTEGER NOT NULL,
            snapshot_date TEXT NOT NULL,
            title TEXT NOT NULL DEFAULT '',
            views INTEGER NOT NULL DEFAULT 0,
            favorites INTEGER NOT NULL DEFAULT 0,
            views_today INTEGER NOT NULL DEFAULT 0,
            favorites_today INTEGER NOT NULL DEFAULT 0,
            units_sold_month INTEGER NOT NULL DEFAULT 0,
            revenue_month REAL NOT NULL DEFAULT 0,
            conversion_rate REAL NOT NULL DEFAULT 0,
            image_url TEXT NOT NULL DEFAULT '',
            listing_url TEXT NOT NULL DEFAULT '',
            PRIMARY KEY(shop_id, listing_id, snapshot_date)
        );

        CREATE TABLE IF NOT EXISTS chart_snapshots (
            shop_id TEXT NOT NULL,
            chart_type TEXT NOT NULL,
            period_start TEXT NOT NULL,
            period_end TEXT NOT NULL,
            image_png_base64 TEXT NOT NULL,
            updated_at TEXT NOT NULL,
            PRIMARY KEY(shop_id, chart_type)
        );

        CREATE TABLE IF NOT EXISTS shop_telegram_settings (
            shop_id TEXT PRIMARY KEY,
            encrypted_bot_token TEXT NOT NULL DEFAULT '',
            chat_id TEXT NOT NULL DEFAULT '',
            is_enabled INTEGER NOT NULL DEFAULT 0,
            notify_on_orders INTEGER NOT NULL DEFAULT 1,
            notify_on_stock INTEGER NOT NULL DEFAULT 1,
            daily_brief_enabled INTEGER NOT NULL DEFAULT 1,
            updated_at TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS shop_carrier_sessions (
            shop_id TEXT NOT NULL,
            carrier_id TEXT NOT NULL,
            encrypted_credentials TEXT NOT NULL DEFAULT '',
            account_no TEXT NOT NULL DEFAULT '',
            service_level TEXT NOT NULL DEFAULT '',
            is_connected INTEGER NOT NULL DEFAULT 0,
            updated_at TEXT NOT NULL,
            PRIMARY KEY(shop_id, carrier_id)
        );

        CREATE TABLE IF NOT EXISTS etsy_app_credentials (
            shop_id TEXT PRIMARY KEY,
            encrypted_keystring TEXT NOT NULL DEFAULT '',
            encrypted_shared_secret TEXT NOT NULL DEFAULT '',
            redirect_uri TEXT NOT NULL DEFAULT '',
            updated_at TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS listing_ai_audits (
            shop_id TEXT NOT NULL,
            listing_id TEXT NOT NULL,
            title TEXT NOT NULL,
            current_seo_score INTEGER NOT NULL DEFAULT 0,
            optimized_seo_score INTEGER NOT NULL DEFAULT 0,
            status TEXT NOT NULL DEFAULT '',
            provider TEXT NOT NULL DEFAULT '',
            model TEXT NOT NULL DEFAULT '',
            result_json TEXT NOT NULL DEFAULT '{}',
            audited_at TEXT NOT NULL,
            PRIMARY KEY(shop_id, listing_id)
        );

        CREATE TABLE IF NOT EXISTS shop_ai_settings (
            shop_id TEXT PRIMARY KEY,
            settings_json TEXT NOT NULL DEFAULT '',
            updated_at TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS shop_payment_reserve (
            shop_id TEXT PRIMARY KEY,
            snapshot_json TEXT NOT NULL DEFAULT '',
            updated_at TEXT NOT NULL
        );
        """;
}
