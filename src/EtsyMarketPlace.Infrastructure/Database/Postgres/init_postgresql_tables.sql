-- =========================================================================
-- EtsyMarketPlace PostgreSQL Schema Initialization Script
-- Target Database: etsy_marketplace_db
-- =========================================================================

-- 1. App Users (Kullanıcılar & AI Kotası)
CREATE TABLE IF NOT EXISTS app_users (
    id VARCHAR(128) PRIMARY KEY,
    username VARCHAR(100) NOT NULL UNIQUE,
    email VARCHAR(255) NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    password_salt TEXT NOT NULL,
    role VARCHAR(50) NOT NULL DEFAULT 'StoreOwner',
    assigned_shop_ids TEXT NOT NULL DEFAULT '[]',
    monthly_ai_token_quota INTEGER NOT NULL DEFAULT 500000,
    used_ai_tokens INTEGER NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_login_at TIMESTAMPTZ NULL
);

-- 2. Audit Logs (Denetim Kayıtları)
CREATE TABLE IF NOT EXISTS audit_logs (
    id BIGSERIAL PRIMARY KEY,
    user_id VARCHAR(128) NOT NULL,
    username VARCHAR(100) NOT NULL,
    action VARCHAR(100) NOT NULL,
    details TEXT NULL,
    ip_address VARCHAR(64) NULL,
    timestamp TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS ix_audit_logs_timestamp ON audit_logs (timestamp);
CREATE INDEX IF NOT EXISTS ix_audit_logs_user_id ON audit_logs (user_id);

-- 3. Shop Connections (Mağaza Kartları)
CREATE TABLE IF NOT EXISTS shop_connections (
    shop_id VARCHAR(64) PRIMARY KEY,
    shop_name VARCHAR(255) NOT NULL DEFAULT '',
    user_id VARCHAR(128) NOT NULL DEFAULT '',
    last_synchronized_at TIMESTAMPTZ NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

-- 4. OAuth Tokens (Etsy v3 OAuth2 Token'ları)
CREATE TABLE IF NOT EXISTS oauth_tokens (
    shop_id VARCHAR(64) PRIMARY KEY REFERENCES shop_connections(shop_id) ON DELETE CASCADE,
    access_token TEXT NOT NULL,
    refresh_token TEXT NOT NULL,
    access_token_expires_at TIMESTAMPTZ NOT NULL,
    token_type VARCHAR(50) NOT NULL DEFAULT 'Bearer'
);

-- 5. Etsy App Credentials (Mağaza API Key ve Secret)
CREATE TABLE IF NOT EXISTS etsy_app_credentials (
    shop_id VARCHAR(64) PRIMARY KEY REFERENCES shop_connections(shop_id) ON DELETE CASCADE,
    encrypted_keystring TEXT NOT NULL DEFAULT '',
    encrypted_shared_secret TEXT NOT NULL DEFAULT '',
    redirect_uri VARCHAR(500) NOT NULL DEFAULT '',
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 6. Sync States (Senkronizasyon İmleçleri)
CREATE TABLE IF NOT EXISTS sync_states (
    shop_id VARCHAR(64) NOT NULL REFERENCES shop_connections(shop_id) ON DELETE CASCADE,
    data_type VARCHAR(64) NOT NULL,
    last_successful_sync_at TIMESTAMPTZ NULL,
    last_cursor_at TIMESTAMPTZ NULL,
    last_error TEXT NULL,
    PRIMARY KEY (shop_id, data_type)
);

-- 7. Bank Payouts (Etsy Banka Hakedişleri)
CREATE TABLE IF NOT EXISTS bank_payouts (
    shop_id VARCHAR(64) NOT NULL REFERENCES shop_connections(shop_id) ON DELETE CASCADE,
    reference_id VARCHAR(128) NOT NULL,
    occurred_at TIMESTAMPTZ NOT NULL,
    amount NUMERIC(18, 4) NOT NULL,
    currency VARCHAR(10) NOT NULL DEFAULT 'USD',
    exchange_rate_to_try NUMERIC(18, 4) NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'PAID',
    description VARCHAR(500) NOT NULL DEFAULT '',
    PRIMARY KEY (shop_id, reference_id)
);
CREATE INDEX IF NOT EXISTS ix_bank_payouts_shop_occurred ON bank_payouts (shop_id, occurred_at);

-- 8. Financial Transactions (Etsy Ödeme Defteri Hareketleri)
CREATE TABLE IF NOT EXISTS financial_transactions (
    shop_id VARCHAR(64) NOT NULL REFERENCES shop_connections(shop_id) ON DELETE CASCADE,
    reference_id VARCHAR(128) NOT NULL,
    occurred_at TIMESTAMPTZ NOT NULL,
    gross_sales NUMERIC(18, 4) NOT NULL DEFAULT 0,
    platform_fees NUMERIC(18, 4) NOT NULL DEFAULT 0,
    internal_ads_cost NUMERIC(18, 4) NOT NULL DEFAULT 0,
    external_ads_cost NUMERIC(18, 4) NOT NULL DEFAULT 0,
    product_cost NUMERIC(18, 4) NOT NULL DEFAULT 0,
    shipping_cost NUMERIC(18, 4) NOT NULL DEFAULT 0,
    refunds NUMERIC(18, 4) NOT NULL DEFAULT 0,
    currency VARCHAR(10) NOT NULL DEFAULT 'USD',
    PRIMARY KEY (shop_id, reference_id)
);
CREATE INDEX IF NOT EXISTS ix_financial_transactions_shop_occurred ON financial_transactions (shop_id, occurred_at);

-- 9. Order Costs (Sipariş Maliyet ve Kâr/Zarar)
CREATE TABLE IF NOT EXISTS order_costs (
    shop_id VARCHAR(64) NOT NULL REFERENCES shop_connections(shop_id) ON DELETE CASCADE,
    order_id VARCHAR(128) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    currency VARCHAR(10) NOT NULL DEFAULT 'USD',
    order_total NUMERIC(18, 4) NOT NULL DEFAULT 0,
    product_cost NUMERIC(18, 4) NULL,
    shipping_cost NUMERIC(18, 4) NULL,
    alert_reason VARCHAR(500) NULL,
    PRIMARY KEY (shop_id, order_id)
);
CREATE INDEX IF NOT EXISTS ix_order_costs_shop_created ON order_costs (shop_id, created_at);

-- 10. Monthly Order Summaries (Aylık Sipariş & Ciro Özeti)
CREATE TABLE IF NOT EXISTS monthly_order_summaries (
    shop_id VARCHAR(64) NOT NULL REFERENCES shop_connections(shop_id) ON DELETE CASCADE,
    year_month VARCHAR(7) NOT NULL,
    order_count INTEGER NOT NULL DEFAULT 0,
    units_sold INTEGER NOT NULL DEFAULT 0,
    gross_revenue NUMERIC(18, 4) NOT NULL DEFAULT 0,
    avg_order_value NUMERIC(18, 4) NOT NULL DEFAULT 0,
    shipped_orders INTEGER NOT NULL DEFAULT 0,
    unfulfilled_orders INTEGER NOT NULL DEFAULT 0,
    currency VARCHAR(10) NOT NULL DEFAULT 'USD',
    PRIMARY KEY (shop_id, year_month)
);

-- 11. Listing Traffic Daily (Günlük Ürün Trafik & Dönüşüm)
CREATE TABLE IF NOT EXISTS listing_traffic_daily (
    shop_id VARCHAR(64) NOT NULL REFERENCES shop_connections(shop_id) ON DELETE CASCADE,
    listing_id BIGINT NOT NULL,
    snapshot_date VARCHAR(10) NOT NULL,
    title VARCHAR(500) NOT NULL DEFAULT '',
    views INTEGER NOT NULL DEFAULT 0,
    favorites INTEGER NOT NULL DEFAULT 0,
    views_today INTEGER NOT NULL DEFAULT 0,
    favorites_today INTEGER NOT NULL DEFAULT 0,
    units_sold_month INTEGER NOT NULL DEFAULT 0,
    revenue_month NUMERIC(18, 4) NOT NULL DEFAULT 0,
    conversion_rate NUMERIC(8, 4) NOT NULL DEFAULT 0,
    image_url VARCHAR(1000) NOT NULL DEFAULT '',
    listing_url VARCHAR(1000) NOT NULL DEFAULT '',
    PRIMARY KEY (shop_id, listing_id, snapshot_date)
);

-- 12. Listing AI Audits (AI SEO Denetimleri)
CREATE TABLE IF NOT EXISTS listing_ai_audits (
    shop_id VARCHAR(64) NOT NULL REFERENCES shop_connections(shop_id) ON DELETE CASCADE,
    listing_id VARCHAR(64) NOT NULL,
    title VARCHAR(500) NOT NULL DEFAULT '',
    current_seo_score INTEGER NOT NULL DEFAULT 0,
    optimized_seo_score INTEGER NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT '',
    provider VARCHAR(50) NOT NULL DEFAULT '',
    model VARCHAR(50) NOT NULL DEFAULT '',
    result_json TEXT NOT NULL DEFAULT '{}',
    audited_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (shop_id, listing_id)
);

-- 13. Shop Telegram Settings (Telegram Bildirim Botu)
CREATE TABLE IF NOT EXISTS shop_telegram_settings (
    shop_id VARCHAR(64) PRIMARY KEY REFERENCES shop_connections(shop_id) ON DELETE CASCADE,
    encrypted_bot_token TEXT NOT NULL DEFAULT '',
    chat_id VARCHAR(100) NOT NULL DEFAULT '',
    is_enabled BOOLEAN NOT NULL DEFAULT FALSE,
    notify_on_orders BOOLEAN NOT NULL DEFAULT TRUE,
    notify_on_stock BOOLEAN NOT NULL DEFAULT TRUE,
    daily_brief_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 14. Shop Carrier Sessions (Kargo Taşıyıcı Oturumları)
CREATE TABLE IF NOT EXISTS shop_carrier_sessions (
    shop_id VARCHAR(64) NOT NULL REFERENCES shop_connections(shop_id) ON DELETE CASCADE,
    carrier_id VARCHAR(50) NOT NULL,
    encrypted_credentials TEXT NOT NULL DEFAULT '',
    account_no VARCHAR(100) NOT NULL DEFAULT '',
    service_level VARCHAR(100) NOT NULL DEFAULT '',
    is_connected BOOLEAN NOT NULL DEFAULT FALSE,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (shop_id, carrier_id)
);

-- 15. Shop AI Settings (Mağaza AI Davranış Ayarları)
CREATE TABLE IF NOT EXISTS shop_ai_settings (
    shop_id VARCHAR(64) PRIMARY KEY REFERENCES shop_connections(shop_id) ON DELETE CASCADE,
    settings_json TEXT NOT NULL DEFAULT '{}',
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 16. Chart Snapshots (Grafik Ön Belleği)
CREATE TABLE IF NOT EXISTS chart_snapshots (
    shop_id VARCHAR(64) NOT NULL REFERENCES shop_connections(shop_id) ON DELETE CASCADE,
    chart_type VARCHAR(50) NOT NULL,
    period_start VARCHAR(30) NOT NULL,
    period_end VARCHAR(30) NOT NULL,
    image_png_base64 TEXT NOT NULL DEFAULT '',
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (shop_id, chart_type)
);
