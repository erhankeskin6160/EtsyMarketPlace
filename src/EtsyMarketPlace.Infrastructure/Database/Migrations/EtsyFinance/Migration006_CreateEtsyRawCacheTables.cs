namespace EtsyMarketPlace.Infrastructure.Database.Migrations.EtsyFinance;

/// <summary>
/// Etsy API DB-First + Delta Sync için ham defter satırları, sipariş fişleri,
/// ledger_entry_id -> receipt_id eşleşme tablosu ve watermark tablolarını oluşturur.
/// </summary>
public sealed class Migration006_CreateEtsyRawCacheTables : SqlMigration
{
    public override int Version => 6;
    public override string Name => "CreateEtsyRawCacheTables";

    public override string UpSql => """
        CREATE TABLE IF NOT EXISTS etsy_ledger_entries_cache (
            shop_id TEXT NOT NULL,
            entry_id TEXT NOT NULL,
            created_unix INTEGER NOT NULL,
            raw_json TEXT NOT NULL,
            updated_at TEXT NOT NULL,
            PRIMARY KEY (shop_id, entry_id)
        );

        CREATE INDEX IF NOT EXISTS ix_etsy_ledger_entries_cache_shop_created
            ON etsy_ledger_entries_cache (shop_id, created_unix);

        CREATE TABLE IF NOT EXISTS etsy_receipts_cache (
            shop_id TEXT NOT NULL,
            receipt_id INTEGER NOT NULL,
            created_unix INTEGER NOT NULL,
            updated_unix INTEGER NOT NULL,
            raw_json TEXT NOT NULL,
            updated_at TEXT NOT NULL,
            PRIMARY KEY (shop_id, receipt_id)
        );

        CREATE INDEX IF NOT EXISTS ix_etsy_receipts_cache_shop_created
            ON etsy_receipts_cache (shop_id, created_unix);

        CREATE TABLE IF NOT EXISTS etsy_ledger_receipt_map (
            shop_id TEXT NOT NULL,
            ledger_entry_id INTEGER NOT NULL,
            receipt_id INTEGER NOT NULL,
            updated_at TEXT NOT NULL,
            PRIMARY KEY (shop_id, ledger_entry_id)
        );

        CREATE TABLE IF NOT EXISTS etsy_sync_watermarks (
            shop_id TEXT NOT NULL,
            dataset_key TEXT NOT NULL,
            min_synced_unix INTEGER NOT NULL,
            max_synced_unix INTEGER NOT NULL,
            last_delta_sync_unix INTEGER NOT NULL,
            updated_at TEXT NOT NULL,
            PRIMARY KEY (shop_id, dataset_key)
        );
        """;
}
