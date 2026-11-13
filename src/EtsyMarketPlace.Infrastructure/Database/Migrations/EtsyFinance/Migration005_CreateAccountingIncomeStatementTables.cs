namespace EtsyMarketPlace.Infrastructure.Database.Migrations.EtsyFinance;

/// <summary>
/// Genel Muhasebe Resmi Gelir Tablosu (TDHP 600–692) SQLite yedek şeması.
/// </summary>
public sealed class Migration005_CreateAccountingIncomeStatementTables : SqlMigration
{
    public override int Version => 5;
    public override string Name => "CreateAccountingIncomeStatementTables";

    public override string UpSql => """
        CREATE TABLE IF NOT EXISTS accounting_shop_profiles (
            shop_id TEXT PRIMARY KEY,
            commission_target INTEGER NOT NULL DEFAULT 0,
            shipping_target INTEGER NOT NULL DEFAULT 0,
            canceled_cost_target INTEGER NOT NULL DEFAULT 0,
            inventory_method INTEGER NOT NULL DEFAULT 0,
            tax_regime INTEGER NOT NULL DEFAULT 0,
            young_entrepreneur_mode_enabled INTEGER NOT NULL DEFAULT 1,
            young_entrepreneur_annual_limit_try TEXT NOT NULL DEFAULT '400000',
            young_entrepreneur_prior_used_try TEXT NOT NULL DEFAULT '0',
            e_export_exemption_rate_pct TEXT NOT NULL DEFAULT '0',
            custom_or_corporate_tax_rate_pct TEXT NOT NULL DEFAULT '25',
            custom_gvk_brackets_json TEXT NULL,
            include_fx_spread_and_exchange_difference INTEGER NOT NULL DEFAULT 1,
            updated_at TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS accounting_operating_expenses (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            shop_id TEXT NOT NULL,
            entry_date TEXT NOT NULL,
            document_no TEXT NOT NULL DEFAULT '',
            category INTEGER NOT NULL,
            description TEXT NOT NULL,
            amount TEXT NOT NULL,
            currency TEXT NOT NULL DEFAULT 'TRY',
            exchange_rate_to_try TEXT NOT NULL DEFAULT '1',
            amount_try TEXT NOT NULL,
            is_tax_deductible INTEGER NOT NULL DEFAULT 1,
            frequency INTEGER NOT NULL DEFAULT 0,
            recurring_end_date TEXT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_accounting_operating_expenses_shop_entry
            ON accounting_operating_expenses (shop_id, entry_date);

        CREATE TABLE IF NOT EXISTS accounting_fixed_assets (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            shop_id TEXT NOT NULL,
            asset_name TEXT NOT NULL,
            asset_account_code TEXT NOT NULL DEFAULT '253',
            expense_target_category INTEGER NOT NULL DEFAULT 632,
            acquisition_date TEXT NOT NULL,
            acquisition_cost_try TEXT NOT NULL,
            salvage_value_try TEXT NOT NULL DEFAULT '0',
            useful_life_years INTEGER NOT NULL DEFAULT 5,
            method INTEGER NOT NULL DEFAULT 0,
            is_active INTEGER NOT NULL DEFAULT 1,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_accounting_fixed_assets_shop
            ON accounting_fixed_assets (shop_id);

        CREATE TABLE IF NOT EXISTS accounting_inventory_adjustments (
            shop_id TEXT NOT NULL,
            year INTEGER NOT NULL,
            month INTEGER NOT NULL,
            opening_inventory_try TEXT NOT NULL DEFAULT '0',
            gross_purchases_try TEXT NOT NULL DEFAULT '0',
            purchase_returns_and_discounts_try TEXT NOT NULL DEFAULT '0',
            closing_inventory_try TEXT NOT NULL DEFAULT '0',
            notes TEXT NULL,
            updated_at TEXT NOT NULL,
            PRIMARY KEY (shop_id, year, month)
        );
        """;
}

