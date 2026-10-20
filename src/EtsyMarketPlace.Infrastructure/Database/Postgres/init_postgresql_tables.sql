-- =========================================================================
-- EtsyMarketPlace PostgreSQL Full Enterprise Schema (31 Tables)
-- Target Database: etsy_marketplace_db
-- =========================================================================

-- -------------------------------------------------------------------------
-- BÖLÜM 1: KULLANICI, GÜVENLİK VE MAĞAZA ÇEKİRDEĞİ (CORE)
-- -------------------------------------------------------------------------

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

-- -------------------------------------------------------------------------
-- BÖLÜM 2: FİNANS, MUHASEBE VE DEFTER MOTORU
-- -------------------------------------------------------------------------

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

-- -------------------------------------------------------------------------
-- BÖLÜM 3: TRAFİK, AI VE ENTEGRASYON AYARLARI
-- -------------------------------------------------------------------------

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

-- -------------------------------------------------------------------------
-- BÖLÜM 4: TÜM EKSTRA YAN MODÜLLER (TAM KADRO 31 TABLO)
-- -------------------------------------------------------------------------

-- 17. Listing A/B Tests (A/B Test Modülü)
CREATE TABLE IF NOT EXISTS listing_ab_tests (
    id BIGSERIAL PRIMARY KEY,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    listing_id VARCHAR(64) NOT NULL DEFAULT '',
    listing_title VARCHAR(500) NOT NULL DEFAULT '',
    experiment_name VARCHAR(255) NOT NULL DEFAULT '',
    variant_a_title TEXT NOT NULL DEFAULT '',
    variant_b_title TEXT NOT NULL DEFAULT '',
    variant_a_tags TEXT NOT NULL DEFAULT '',
    variant_b_tags TEXT NOT NULL DEFAULT '',
    variant_a_description TEXT NOT NULL DEFAULT '',
    variant_b_description TEXT NOT NULL DEFAULT '',
    start_date TIMESTAMPTZ NOT NULL,
    end_date TIMESTAMPTZ NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Active',
    before_views INTEGER NOT NULL DEFAULT 0,
    before_favorites INTEGER NOT NULL DEFAULT 0,
    before_sales INTEGER NOT NULL DEFAULT 0,
    after_views INTEGER NOT NULL DEFAULT 0,
    after_favorites INTEGER NOT NULL DEFAULT 0,
    after_sales INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_listing_ab_tests_created ON listing_ab_tests(created_at DESC);
CREATE INDEX IF NOT EXISTS ix_listing_ab_tests_listing ON listing_ab_tests(listing_id);

-- 18. AI Usage History (AI Token ve Maliyet Tüketim Kaydı)
CREATE TABLE IF NOT EXISTS ai_usage_history (
    id BIGSERIAL PRIMARY KEY,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    module_name VARCHAR(100) NOT NULL DEFAULT '',
    provider VARCHAR(50) NOT NULL DEFAULT '',
    model_name VARCHAR(100) NOT NULL DEFAULT '',
    prompt_tokens INTEGER NOT NULL DEFAULT 0,
    completion_tokens INTEGER NOT NULL DEFAULT 0,
    total_tokens INTEGER NOT NULL DEFAULT 0,
    cost_usd NUMERIC(18, 6) NOT NULL DEFAULT 0,
    cost_try NUMERIC(18, 6) NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'Başarılı',
    note TEXT NULL
);
CREATE INDEX IF NOT EXISTS ix_ai_usage_history_created ON ai_usage_history(created_at DESC);
CREATE INDEX IF NOT EXISTS ix_ai_usage_history_provider ON ai_usage_history(provider);

-- 19. AI Remote Usage Cache (Bakiye Önbelleği)
CREATE TABLE IF NOT EXISTS ai_remote_usage_cache (
    provider VARCHAR(50) PRIMARY KEY,
    balance_usd NUMERIC(18, 4) NOT NULL DEFAULT 0,
    currency VARCHAR(10) NOT NULL DEFAULT 'USD',
    cached_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    details_json TEXT NULL
);

-- 20. Batch Queue Items (Toplu İşlem Kuyruğu)
CREATE TABLE IF NOT EXISTS batch_queue_items (
    id BIGSERIAL PRIMARY KEY,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    listing_id VARCHAR(64) NOT NULL DEFAULT '',
    original_title TEXT NOT NULL DEFAULT '',
    original_description TEXT NOT NULL DEFAULT '',
    original_tags TEXT NOT NULL DEFAULT '',
    target_keyword VARCHAR(255) NOT NULL DEFAULT '',
    category VARCHAR(255) NOT NULL DEFAULT '',
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    overall_score INTEGER NOT NULL DEFAULT 0,
    optimized_title TEXT NOT NULL DEFAULT '',
    optimized_description TEXT NOT NULL DEFAULT '',
    optimized_tags TEXT NOT NULL DEFAULT '',
    optimized_materials TEXT NOT NULL DEFAULT '',
    risk_warnings TEXT NOT NULL DEFAULT '',
    issues TEXT NOT NULL DEFAULT '',
    processed_at TIMESTAMPTZ NULL,
    error_message TEXT NULL
);
CREATE INDEX IF NOT EXISTS ix_batch_queue_status ON batch_queue_items(status);

-- 21. Order Addresses (Sipariş Adresleri & Kargo Ayrıştırma)
CREATE TABLE IF NOT EXISTS order_addresses (
    receipt_id BIGINT PRIMARY KEY,
    buyer_name VARCHAR(255) NOT NULL DEFAULT '',
    buyer_email VARCHAR(255) NOT NULL DEFAULT '',
    phone VARCHAR(100) NOT NULL DEFAULT '',
    street_address TEXT NOT NULL DEFAULT '',
    second_address TEXT NOT NULL DEFAULT '',
    city VARCHAR(100) NOT NULL DEFAULT '',
    state VARCHAR(100) NOT NULL DEFAULT '',
    postal_code VARCHAR(50) NOT NULL DEFAULT '',
    country_code VARCHAR(10) NOT NULL DEFAULT '',
    country_name VARCHAR(100) NOT NULL DEFAULT '',
    source VARCHAR(50) NOT NULL DEFAULT '',
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ioss_number VARCHAR(100) NOT NULL DEFAULT '',
    product_cost_usd NUMERIC(18, 4) NOT NULL DEFAULT 0,
    shipping_cost_usd NUMERIC(18, 4) NOT NULL DEFAULT 0,
    weight_kg NUMERIC(10, 3) NOT NULL DEFAULT 0,
    width_cm NUMERIC(10, 2) NOT NULL DEFAULT 0,
    length_cm NUMERIC(10, 2) NOT NULL DEFAULT 0,
    height_cm NUMERIC(10, 2) NOT NULL DEFAULT 0,
    gtip_code VARCHAR(50) NOT NULL DEFAULT '',
    selected_carrier VARCHAR(50) NOT NULL DEFAULT '',
    carrier_service_name VARCHAR(100) NOT NULL DEFAULT '',
    tracking_code VARCHAR(100) NOT NULL DEFAULT '',
    label_url TEXT NOT NULL DEFAULT ''
);

ALTER TABLE order_addresses ADD COLUMN IF NOT EXISTS gtip_code VARCHAR(50) NOT NULL DEFAULT '';
ALTER TABLE order_addresses ADD COLUMN IF NOT EXISTS selected_carrier VARCHAR(50) NOT NULL DEFAULT '';
ALTER TABLE order_addresses ADD COLUMN IF NOT EXISTS carrier_service_name VARCHAR(100) NOT NULL DEFAULT '';
ALTER TABLE order_addresses ADD COLUMN IF NOT EXISTS tracking_code VARCHAR(100) NOT NULL DEFAULT '';
ALTER TABLE order_addresses ADD COLUMN IF NOT EXISTS label_url TEXT NOT NULL DEFAULT '';

-- 22. Tracking Items (Kargo Takip Öğeleri)
CREATE TABLE IF NOT EXISTS tracking_items (
    id BIGSERIAL PRIMARY KEY,
    entity_type INTEGER NOT NULL,
    external_key VARCHAR(128) NOT NULL,
    display_name VARCHAR(255) NOT NULL,
    url TEXT NOT NULL DEFAULT '',
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_tracking_items_type_key UNIQUE (entity_type, external_key)
);

-- 23. Tracking Snapshots (Kargo Takip Geçmişi)
CREATE TABLE IF NOT EXISTS tracking_snapshots (
    id BIGSERIAL PRIMARY KEY,
    tracking_item_id BIGINT NOT NULL REFERENCES tracking_items(id) ON DELETE CASCADE,
    captured_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    price NUMERIC(18, 4) NULL,
    currency_code VARCHAR(10) NOT NULL DEFAULT '',
    favorites INTEGER NULL,
    views INTEGER NULL,
    shop_sales INTEGER NULL,
    review_count INTEGER NULL,
    review_average NUMERIC(5, 2) NULL,
    seo_score INTEGER NULL,
    market_score INTEGER NULL,
    demand_score INTEGER NULL,
    competition_score INTEGER NULL,
    opportunity_score INTEGER NULL
);
CREATE INDEX IF NOT EXISTS ix_tracking_snapshots_item ON tracking_snapshots(tracking_item_id);

-- 24. Shop Performance Snapshots (Mağaza Performans Geçmişi)
CREATE TABLE IF NOT EXISTS shop_performance_snapshots (
    id BIGSERIAL PRIMARY KEY,
    shop_id BIGINT NOT NULL,
    shop_name VARCHAR(255) NOT NULL,
    shop_url TEXT NOT NULL DEFAULT '',
    period_start VARCHAR(30) NOT NULL,
    period_end VARCHAR(30) NOT NULL,
    captured_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    capture_day VARCHAR(10) NOT NULL,
    currency_code VARCHAR(10) NOT NULL,
    order_count INTEGER NOT NULL,
    units_sold INTEGER NOT NULL,
    gross_revenue NUMERIC(18, 4) NOT NULL,
    average_order_value NUMERIC(18, 4) NOT NULL,
    product_count INTEGER NOT NULL,
    CONSTRAINT uq_shop_perf_capture UNIQUE (shop_id, period_start, period_end, capture_day)
);

-- 25. Shop Product Performance Snapshots (Ürün Performans Geçmişi)
CREATE TABLE IF NOT EXISTS shop_product_performance_snapshots (
    id BIGSERIAL PRIMARY KEY,
    snapshot_id BIGINT NOT NULL REFERENCES shop_performance_snapshots(id) ON DELETE CASCADE,
    listing_id BIGINT NOT NULL,
    title VARCHAR(500) NOT NULL,
    order_count INTEGER NOT NULL,
    units_sold INTEGER NOT NULL,
    revenue NUMERIC(18, 4) NOT NULL
);

-- 26. Listing Optimization History (SEO Geçmişi)
CREATE TABLE IF NOT EXISTS listing_optimization_history (
    id BIGSERIAL PRIMARY KEY,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    listing_id VARCHAR(64) NOT NULL DEFAULT '',
    listing_title VARCHAR(500) NOT NULL DEFAULT '',
    target_keyword VARCHAR(255) NOT NULL DEFAULT '',
    current_seo_score INTEGER NOT NULL,
    optimized_seo_score INTEGER NOT NULL,
    suggested_title TEXT NOT NULL DEFAULT '',
    suggested_tags TEXT NOT NULL DEFAULT '',
    description_draft TEXT NOT NULL DEFAULT '',
    risk_warnings TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS ix_listing_opt_created ON listing_optimization_history(created_at DESC);

-- 27. Vault Sessions (ShopVault - Kasa Oturumları)
CREATE TABLE IF NOT EXISTS vault_sessions (
    session_id VARCHAR(128) PRIMARY KEY,
    shop_id BIGINT NOT NULL,
    shop_name VARCHAR(255) NOT NULL,
    shop_url TEXT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    completed_at_utc TIMESTAMPTZ NULL,
    total_listings INTEGER NOT NULL DEFAULT 0,
    total_images INTEGER NOT NULL DEFAULT 0,
    total_size_bytes BIGINT NOT NULL DEFAULT 0,
    archive_zip_path TEXT NULL,
    status VARCHAR(50) NOT NULL,
    error_message TEXT NULL
);

-- 28. Vault Listings (ShopVault - Yedek Ürünler)
CREATE TABLE IF NOT EXISTS vault_listings (
    session_id VARCHAR(128) NOT NULL REFERENCES vault_sessions(session_id) ON DELETE CASCADE,
    listing_id BIGINT NOT NULL,
    original_shop_id BIGINT NOT NULL,
    original_shop_name VARCHAR(255) NOT NULL,
    title TEXT NOT NULL,
    description TEXT NULL,
    price NUMERIC(18, 4) NOT NULL,
    currency VARCHAR(10) NOT NULL,
    quantity INTEGER NOT NULL,
    tags_json TEXT NULL,
    materials_json TEXT NULL,
    PRIMARY KEY (session_id, listing_id)
);

-- 29. Vault Images (ShopVault - Yedek Fotoğraflar)
CREATE TABLE IF NOT EXISTS vault_images (
    id VARCHAR(128) PRIMARY KEY,
    session_id VARCHAR(128) NOT NULL REFERENCES vault_sessions(session_id) ON DELETE CASCADE,
    listing_id BIGINT NOT NULL,
    original_image_id BIGINT NOT NULL,
    rank INTEGER NOT NULL,
    local_relative_path TEXT NOT NULL,
    original_url TEXT NULL,
    hex_code VARCHAR(50) NULL,
    file_size_bytes BIGINT NOT NULL DEFAULT 0,
    width INTEGER NOT NULL DEFAULT 0,
    height INTEGER NOT NULL DEFAULT 0,
    downloaded_at_utc TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 30. Vault Variations (ShopVault - Varyasyonlar)
CREATE TABLE IF NOT EXISTS vault_variations (
    id VARCHAR(128) PRIMARY KEY,
    session_id VARCHAR(128) NOT NULL REFERENCES vault_sessions(session_id) ON DELETE CASCADE,
    listing_id BIGINT NOT NULL,
    property_id BIGINT NOT NULL,
    property_name VARCHAR(255) NOT NULL,
    value_id BIGINT NOT NULL,
    value_name VARCHAR(255) NOT NULL,
    price_difference NUMERIC(18, 4) NOT NULL DEFAULT 0,
    sku VARCHAR(100) NULL,
    stock_quantity INTEGER NOT NULL DEFAULT 999
);

-- 31. Discovered 3D Models (Viral 3D Model Radarı)
CREATE TABLE IF NOT EXISTS discovered_models (
    external_id VARCHAR(128) PRIMARY KEY,
    platform VARCHAR(50) NOT NULL,
    title VARCHAR(500) NOT NULL,
    description TEXT NULL,
    author VARCHAR(255) NULL,
    model_url TEXT NULL,
    primary_image_url TEXT NULL,
    category VARCHAR(100) NULL,
    tags_csv TEXT NULL,
    downloads_24h INTEGER NOT NULL DEFAULT 0,
    total_downloads INTEGER NOT NULL DEFAULT 0,
    prints_count INTEGER NOT NULL DEFAULT 0,
    likes_count INTEGER NOT NULL DEFAULT 0,
    license_name VARCHAR(100) NULL,
    is_commercial BOOLEAN NOT NULL DEFAULT FALSE,
    filament_grams NUMERIC(10, 2) NOT NULL DEFAULT 0,
    print_time_mins INTEGER NOT NULL DEFAULT 0,
    has_multicolor BOOLEAN NOT NULL DEFAULT FALSE,
    color_count INTEGER NOT NULL DEFAULT 1,
    etsy_competition INTEGER NOT NULL DEFAULT -1,
    opportunity_score INTEGER NOT NULL DEFAULT 50,
    first_seen_utc TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_seen_utc TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS ix_discovered_models_platform ON discovered_models(platform);
CREATE INDEX IF NOT EXISTS ix_discovered_models_score ON discovered_models(opportunity_score DESC);
