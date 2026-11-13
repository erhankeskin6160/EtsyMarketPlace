using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Accounting;
using EtsyMarketPlace.Domain.Accounting;
using EtsyMarketPlace.Domain.Accounting.Entities;
using EtsyMarketPlace.Infrastructure.Database.Migrations;
using EtsyMarketPlace.Infrastructure.Database.Migrations.EtsyFinance;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EtsyMarketPlace.Infrastructure.Persistence.Repositories;

/// <summary>
/// Genel Muhasebe (TDHP 600–692) kayıtları için SQLite yerel deposu ve PostgreSQL çevrimdışı/yedek katmanı.
/// </summary>
public sealed class SqliteAccountingStore : IAccountingRepository, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly ISqliteMigrationRunner _migrationRunner;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private bool _initialized;

    [ActivatorUtilitiesConstructor]
    public SqliteAccountingStore(
        IOptions<EtsyIntegrationOptions> options,
        ISqliteMigrationRunner? migrationRunner = null)
        : this(options.Value.ResolveDatabasePath(), migrationRunner)
    {
    }

    public SqliteAccountingStore(string databasePath, ISqliteMigrationRunner? migrationRunner = null)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();

        _migrationRunner = migrationRunner ?? new SqliteMigrationRunner();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;
        await _schemaLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;
            await _migrationRunner.MigrateAsync(_connectionString, EtsyFinanceMigrations.All, cancellationToken);
            _initialized = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    public async Task<ShopAccountingProfile> GetOrDefaultProfileAsync(string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT shop_id, commission_target, shipping_target, canceled_cost_target,
                   inventory_method, tax_regime, young_entrepreneur_mode_enabled,
                   young_entrepreneur_annual_limit_try, young_entrepreneur_prior_used_try,
                   e_export_exemption_rate_pct, custom_or_corporate_tax_rate_pct,
                   custom_gvk_brackets_json, include_fx_spread_and_exchange_difference, updated_at
            FROM accounting_shop_profiles
            WHERE shop_id = $shop_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$shop_id", key);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? MapProfile(reader)
            : new ShopAccountingProfile { ShopId = key };
    }

    public async Task<ShopAccountingProfile> UpsertProfileAsync(ShopAccountingProfile profile, CancellationToken cancellationToken = default)
    {
        var key = profile.ShopId.Trim();
        var updatedAt = DateTime.UtcNow;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO accounting_shop_profiles (
                shop_id, commission_target, shipping_target, canceled_cost_target,
                inventory_method, tax_regime, young_entrepreneur_mode_enabled,
                young_entrepreneur_annual_limit_try, young_entrepreneur_prior_used_try,
                e_export_exemption_rate_pct, custom_or_corporate_tax_rate_pct,
                custom_gvk_brackets_json, include_fx_spread_and_exchange_difference, updated_at
            )
            VALUES (
                $shop_id, $commission_target, $shipping_target, $canceled_cost_target,
                $inventory_method, $tax_regime, $young_enabled,
                $young_limit, $young_prior,
                $e_export_pct, $custom_tax_pct,
                $custom_brackets, $include_fx, $updated_at
            )
            ON CONFLICT(shop_id) DO UPDATE SET
                commission_target = excluded.commission_target,
                shipping_target = excluded.shipping_target,
                canceled_cost_target = excluded.canceled_cost_target,
                inventory_method = excluded.inventory_method,
                tax_regime = excluded.tax_regime,
                young_entrepreneur_mode_enabled = excluded.young_entrepreneur_mode_enabled,
                young_entrepreneur_annual_limit_try = excluded.young_entrepreneur_annual_limit_try,
                young_entrepreneur_prior_used_try = excluded.young_entrepreneur_prior_used_try,
                e_export_exemption_rate_pct = excluded.e_export_exemption_rate_pct,
                custom_or_corporate_tax_rate_pct = excluded.custom_or_corporate_tax_rate_pct,
                custom_gvk_brackets_json = excluded.custom_gvk_brackets_json,
                include_fx_spread_and_exchange_difference = excluded.include_fx_spread_and_exchange_difference,
                updated_at = excluded.updated_at;
            """;

        command.Parameters.AddWithValue("$shop_id", key);
        command.Parameters.AddWithValue("$commission_target", (int)profile.CommissionTarget);
        command.Parameters.AddWithValue("$shipping_target", (int)profile.ShippingTarget);
        command.Parameters.AddWithValue("$canceled_cost_target", (int)profile.CanceledCostTarget);
        command.Parameters.AddWithValue("$inventory_method", (int)profile.InventoryMethod);
        command.Parameters.AddWithValue("$tax_regime", (int)profile.TaxRegime);
        command.Parameters.AddWithValue("$young_enabled", profile.YoungEntrepreneurModeEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$young_limit", FormatDecimal(profile.YoungEntrepreneurAnnualLimitTry));
        command.Parameters.AddWithValue("$young_prior", FormatDecimal(profile.YoungEntrepreneurPriorUsedTry));
        command.Parameters.AddWithValue("$e_export_pct", FormatDecimal(profile.EExportExemptionRatePct));
        command.Parameters.AddWithValue("$custom_tax_pct", FormatDecimal(profile.CustomOrCorporateTaxRatePct));
        command.Parameters.AddWithValue("$custom_brackets", (object?)profile.CustomGvkBracketsJson ?? DBNull.Value);
        command.Parameters.AddWithValue("$include_fx", profile.IncludeFxSpreadAndExchangeDifference ? 1 : 0);
        command.Parameters.AddWithValue("$updated_at", FormatUtc(updatedAt));

        await command.ExecuteNonQueryAsync(cancellationToken);

        profile.ShopId = key;
        profile.UpdatedAt = updatedAt;
        return profile;
    }

    public async Task<IReadOnlyList<OperatingExpenseEntry>> GetOperatingExpensesAsync(string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, shop_id, entry_date, document_no, category, description,
                   amount, currency, exchange_rate_to_try, amount_try,
                   is_tax_deductible, frequency, recurring_end_date, created_at, updated_at
            FROM accounting_operating_expenses
            WHERE shop_id = $shop_id
            ORDER BY entry_date DESC, id DESC;
            """;
        command.Parameters.AddWithValue("$shop_id", key);

        var list = new List<OperatingExpenseEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            list.Add(MapExpense(reader));
        return list;
    }

    public async Task<OperatingExpenseEntry?> GetOperatingExpenseByIdAsync(long id, string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, shop_id, entry_date, document_no, category, description,
                   amount, currency, exchange_rate_to_try, amount_try,
                   is_tax_deductible, frequency, recurring_end_date, created_at, updated_at
            FROM accounting_operating_expenses
            WHERE id = $id AND shop_id = $shop_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$shop_id", key);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapExpense(reader) : null;
    }

    public async Task<OperatingExpenseEntry> AddOperatingExpenseAsync(OperatingExpenseEntry entry, CancellationToken cancellationToken = default)
    {
        entry.ShopId = entry.ShopId.Trim();
        entry.EntryDate = EnsureUtc(entry.EntryDate);
        entry.RecurringEndDate = entry.RecurringEndDate.HasValue ? EnsureUtc(entry.RecurringEndDate.Value) : null;
        entry.CreatedAt = DateTime.UtcNow;
        entry.UpdatedAt = DateTime.UtcNow;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO accounting_operating_expenses (
                shop_id, entry_date, document_no, category, description,
                amount, currency, exchange_rate_to_try, amount_try,
                is_tax_deductible, frequency, recurring_end_date, created_at, updated_at
            )
            VALUES (
                $shop_id, $entry_date, $document_no, $category, $description,
                $amount, $currency, $exchange_rate_to_try, $amount_try,
                $is_tax_deductible, $frequency, $recurring_end_date, $created_at, $updated_at
            );
            SELECT last_insert_rowid();
            """;
        BindExpenseParameters(command, entry);
        command.Parameters.AddWithValue("$created_at", FormatUtc(entry.CreatedAt));

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        entry.Id = Convert.ToInt64(scalar ?? 0L, CultureInfo.InvariantCulture);
        return entry;
    }

    public async Task<OperatingExpenseEntry?> UpdateOperatingExpenseAsync(OperatingExpenseEntry entry, CancellationToken cancellationToken = default)
    {
        entry.ShopId = entry.ShopId.Trim();
        entry.EntryDate = EnsureUtc(entry.EntryDate);
        entry.RecurringEndDate = entry.RecurringEndDate.HasValue ? EnsureUtc(entry.RecurringEndDate.Value) : null;
        entry.UpdatedAt = DateTime.UtcNow;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE accounting_operating_expenses SET
                entry_date = $entry_date,
                document_no = $document_no,
                category = $category,
                description = $description,
                amount = $amount,
                currency = $currency,
                exchange_rate_to_try = $exchange_rate_to_try,
                amount_try = $amount_try,
                is_tax_deductible = $is_tax_deductible,
                frequency = $frequency,
                recurring_end_date = $recurring_end_date,
                updated_at = $updated_at
            WHERE id = $id AND shop_id = $shop_id;
            """;
        command.Parameters.AddWithValue("$id", entry.Id);
        BindExpenseParameters(command, entry);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        return rows > 0 ? await GetOperatingExpenseByIdAsync(entry.Id, entry.ShopId, cancellationToken) : null;
    }

    public async Task<bool> DeleteOperatingExpenseAsync(long id, string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM accounting_operating_expenses WHERE id = $id AND shop_id = $shop_id;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$shop_id", key);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<IReadOnlyList<FixedAssetItem>> GetFixedAssetsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, shop_id, asset_name, asset_account_code, expense_target_category,
                   acquisition_date, acquisition_cost_try, salvage_value_try,
                   useful_life_years, method, is_active, created_at, updated_at
            FROM accounting_fixed_assets
            WHERE shop_id = $shop_id
            ORDER BY acquisition_date DESC, id DESC;
            """;
        command.Parameters.AddWithValue("$shop_id", key);

        var list = new List<FixedAssetItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            list.Add(MapFixedAsset(reader));
        return list;
    }

    public async Task<FixedAssetItem?> GetFixedAssetByIdAsync(long id, string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, shop_id, asset_name, asset_account_code, expense_target_category,
                   acquisition_date, acquisition_cost_try, salvage_value_try,
                   useful_life_years, method, is_active, created_at, updated_at
            FROM accounting_fixed_assets
            WHERE id = $id AND shop_id = $shop_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$shop_id", key);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapFixedAsset(reader) : null;
    }

    public async Task<FixedAssetItem> AddFixedAssetAsync(FixedAssetItem asset, CancellationToken cancellationToken = default)
    {
        asset.ShopId = asset.ShopId.Trim();
        asset.AcquisitionDate = EnsureUtc(asset.AcquisitionDate);
        asset.CreatedAt = DateTime.UtcNow;
        asset.UpdatedAt = DateTime.UtcNow;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO accounting_fixed_assets (
                shop_id, asset_name, asset_account_code, expense_target_category,
                acquisition_date, acquisition_cost_try, salvage_value_try,
                useful_life_years, method, is_active, created_at, updated_at
            )
            VALUES (
                $shop_id, $asset_name, $asset_account_code, $expense_target_category,
                $acquisition_date, $acquisition_cost_try, $salvage_value_try,
                $useful_life_years, $method, $is_active, $created_at, $updated_at
            );
            SELECT last_insert_rowid();
            """;
        BindFixedAssetParameters(command, asset);
        command.Parameters.AddWithValue("$created_at", FormatUtc(asset.CreatedAt));

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        asset.Id = Convert.ToInt64(scalar ?? 0L, CultureInfo.InvariantCulture);
        return asset;
    }

    public async Task<FixedAssetItem?> UpdateFixedAssetAsync(FixedAssetItem asset, CancellationToken cancellationToken = default)
    {
        asset.ShopId = asset.ShopId.Trim();
        asset.AcquisitionDate = EnsureUtc(asset.AcquisitionDate);
        asset.UpdatedAt = DateTime.UtcNow;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE accounting_fixed_assets SET
                asset_name = $asset_name,
                asset_account_code = $asset_account_code,
                expense_target_category = $expense_target_category,
                acquisition_date = $acquisition_date,
                acquisition_cost_try = $acquisition_cost_try,
                salvage_value_try = $salvage_value_try,
                useful_life_years = $useful_life_years,
                method = $method,
                is_active = $is_active,
                updated_at = $updated_at
            WHERE id = $id AND shop_id = $shop_id;
            """;
        command.Parameters.AddWithValue("$id", asset.Id);
        BindFixedAssetParameters(command, asset);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        return rows > 0 ? await GetFixedAssetByIdAsync(asset.Id, asset.ShopId, cancellationToken) : null;
    }

    public async Task<bool> DeleteFixedAssetAsync(long id, string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM accounting_fixed_assets WHERE id = $id AND shop_id = $shop_id;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$shop_id", key);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<IReadOnlyList<InventoryPeriodAdjustment>> GetInventoryAdjustmentsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT shop_id, year, month, opening_inventory_try, gross_purchases_try,
                   purchase_returns_and_discounts_try, closing_inventory_try, notes, updated_at
            FROM accounting_inventory_adjustments
            WHERE shop_id = $shop_id
            ORDER BY year DESC, month DESC;
            """;
        command.Parameters.AddWithValue("$shop_id", key);

        var list = new List<InventoryPeriodAdjustment>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            list.Add(MapAdjustment(reader));
        return list;
    }

    public async Task<InventoryPeriodAdjustment?> GetInventoryAdjustmentAsync(string shopId, int year, int month, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT shop_id, year, month, opening_inventory_try, gross_purchases_try,
                   purchase_returns_and_discounts_try, closing_inventory_try, notes, updated_at
            FROM accounting_inventory_adjustments
            WHERE shop_id = $shop_id AND year = $year AND month = $month
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$shop_id", key);
        command.Parameters.AddWithValue("$year", year);
        command.Parameters.AddWithValue("$month", month);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapAdjustment(reader) : null;
    }

    public async Task<InventoryPeriodAdjustment> UpsertInventoryAdjustmentAsync(InventoryPeriodAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        var key = adjustment.ShopId.Trim();
        var updatedAt = DateTime.UtcNow;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO accounting_inventory_adjustments (
                shop_id, year, month, opening_inventory_try, gross_purchases_try,
                purchase_returns_and_discounts_try, closing_inventory_try, notes, updated_at
            )
            VALUES (
                $shop_id, $year, $month, $opening, $purchases, $returns, $closing, $notes, $updated_at
            )
            ON CONFLICT(shop_id, year, month) DO UPDATE SET
                opening_inventory_try = excluded.opening_inventory_try,
                gross_purchases_try = excluded.gross_purchases_try,
                purchase_returns_and_discounts_try = excluded.purchase_returns_and_discounts_try,
                closing_inventory_try = excluded.closing_inventory_try,
                notes = excluded.notes,
                updated_at = excluded.updated_at;
            """;

        command.Parameters.AddWithValue("$shop_id", key);
        command.Parameters.AddWithValue("$year", adjustment.Year);
        command.Parameters.AddWithValue("$month", adjustment.Month);
        command.Parameters.AddWithValue("$opening", FormatDecimal(adjustment.OpeningInventoryTry));
        command.Parameters.AddWithValue("$purchases", FormatDecimal(adjustment.GrossPurchasesTry));
        command.Parameters.AddWithValue("$returns", FormatDecimal(adjustment.PurchaseReturnsAndDiscountsTry));
        command.Parameters.AddWithValue("$closing", FormatDecimal(adjustment.ClosingInventoryTry));
        command.Parameters.AddWithValue("$notes", (object?)adjustment.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$updated_at", FormatUtc(updatedAt));

        await command.ExecuteNonQueryAsync(cancellationToken);

        adjustment.ShopId = key;
        adjustment.UpdatedAt = updatedAt;
        return adjustment;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static void BindExpenseParameters(SqliteCommand command, OperatingExpenseEntry entry)
    {
        command.Parameters.AddWithValue("$shop_id", entry.ShopId);
        command.Parameters.AddWithValue("$entry_date", FormatUtc(entry.EntryDate));
        command.Parameters.AddWithValue("$document_no", entry.DocumentNo ?? string.Empty);
        command.Parameters.AddWithValue("$category", (int)entry.Category);
        command.Parameters.AddWithValue("$description", entry.Description ?? string.Empty);
        command.Parameters.AddWithValue("$amount", FormatDecimal(entry.Amount));
        command.Parameters.AddWithValue("$currency", entry.Currency ?? "TRY");
        command.Parameters.AddWithValue("$exchange_rate_to_try", FormatDecimal(entry.ExchangeRateToTry));
        command.Parameters.AddWithValue("$amount_try", FormatDecimal(entry.AmountTry));
        command.Parameters.AddWithValue("$is_tax_deductible", entry.IsTaxDeductible ? 1 : 0);
        command.Parameters.AddWithValue("$frequency", (int)entry.Frequency);
        command.Parameters.AddWithValue("$recurring_end_date", entry.RecurringEndDate.HasValue ? FormatUtc(entry.RecurringEndDate.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$updated_at", FormatUtc(entry.UpdatedAt));
    }

    private static void BindFixedAssetParameters(SqliteCommand command, FixedAssetItem asset)
    {
        command.Parameters.AddWithValue("$shop_id", asset.ShopId);
        command.Parameters.AddWithValue("$asset_name", asset.AssetName ?? string.Empty);
        command.Parameters.AddWithValue("$asset_account_code", asset.AssetAccountCode ?? "253");
        command.Parameters.AddWithValue("$expense_target_category", (int)asset.ExpenseTargetCategory);
        command.Parameters.AddWithValue("$acquisition_date", FormatUtc(asset.AcquisitionDate));
        command.Parameters.AddWithValue("$acquisition_cost_try", FormatDecimal(asset.AcquisitionCostTry));
        command.Parameters.AddWithValue("$salvage_value_try", FormatDecimal(asset.SalvageValueTry));
        command.Parameters.AddWithValue("$useful_life_years", asset.UsefulLifeYears);
        command.Parameters.AddWithValue("$method", (int)asset.Method);
        command.Parameters.AddWithValue("$is_active", asset.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$updated_at", FormatUtc(asset.UpdatedAt));
    }

    private static ShopAccountingProfile MapProfile(DbDataReader reader) => new()
    {
        ShopId = reader.GetString(0),
        CommissionTarget = (CommissionAccountTarget)reader.GetInt32(1),
        ShippingTarget = (ShippingPackagingCostTarget)reader.GetInt32(2),
        CanceledCostTarget = (CanceledOrderCostTarget)reader.GetInt32(3),
        InventoryMethod = (InventoryValuationMethod)reader.GetInt32(4),
        TaxRegime = (TaxRegimeType)reader.GetInt32(5),
        YoungEntrepreneurModeEnabled = reader.GetInt32(6) != 0,
        YoungEntrepreneurAnnualLimitTry = ReadDecimal(reader, 7, 400_000m),
        YoungEntrepreneurPriorUsedTry = ReadDecimal(reader, 8, 0m),
        EExportExemptionRatePct = ReadDecimal(reader, 9, 0m),
        CustomOrCorporateTaxRatePct = ReadDecimal(reader, 10, 25m),
        CustomGvkBracketsJson = reader.IsDBNull(11) ? null : reader.GetString(11),
        IncludeFxSpreadAndExchangeDifference = reader.GetInt32(12) != 0,
        UpdatedAt = ReadUtcDateTime(reader, 13)
    };

    private static OperatingExpenseEntry MapExpense(DbDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        ShopId = reader.GetString(1),
        EntryDate = ReadUtcDateTime(reader, 2),
        DocumentNo = reader.GetString(3),
        Category = (OperatingExpenseCategory)reader.GetInt32(4),
        Description = reader.GetString(5),
        Amount = ReadDecimal(reader, 6, 0m),
        Currency = reader.GetString(7),
        ExchangeRateToTry = ReadDecimal(reader, 8, 1m),
        AmountTry = ReadDecimal(reader, 9, 0m),
        IsTaxDeductible = reader.GetInt32(10) != 0,
        Frequency = (ExpenseFrequency)reader.GetInt32(11),
        RecurringEndDate = reader.IsDBNull(12) ? null : ReadUtcDateTime(reader, 12),
        CreatedAt = ReadUtcDateTime(reader, 13),
        UpdatedAt = ReadUtcDateTime(reader, 14)
    };

    private static FixedAssetItem MapFixedAsset(DbDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        ShopId = reader.GetString(1),
        AssetName = reader.GetString(2),
        AssetAccountCode = reader.GetString(3),
        ExpenseTargetCategory = (OperatingExpenseCategory)reader.GetInt32(4),
        AcquisitionDate = ReadUtcDateTime(reader, 5),
        AcquisitionCostTry = ReadDecimal(reader, 6, 0m),
        SalvageValueTry = ReadDecimal(reader, 7, 0m),
        UsefulLifeYears = reader.GetInt32(8),
        Method = (DepreciationMethod)reader.GetInt32(9),
        IsActive = reader.GetInt32(10) != 0,
        CreatedAt = ReadUtcDateTime(reader, 11),
        UpdatedAt = ReadUtcDateTime(reader, 12)
    };

    private static InventoryPeriodAdjustment MapAdjustment(DbDataReader reader) => new()
    {
        ShopId = reader.GetString(0),
        Year = reader.GetInt32(1),
        Month = reader.GetInt32(2),
        OpeningInventoryTry = ReadDecimal(reader, 3, 0m),
        GrossPurchasesTry = ReadDecimal(reader, 4, 0m),
        PurchaseReturnsAndDiscountsTry = ReadDecimal(reader, 5, 0m),
        ClosingInventoryTry = ReadDecimal(reader, 6, 0m),
        Notes = reader.IsDBNull(7) ? null : reader.GetString(7),
        UpdatedAt = ReadUtcDateTime(reader, 8)
    };

    private static decimal ReadDecimal(DbDataReader reader, int ordinal, decimal fallback)
    {
        if (reader.IsDBNull(ordinal)) return fallback;
        var raw = reader.GetValue(ordinal);
        if (raw is decimal d) return d;
        if (raw is double dbl) return Convert.ToDecimal(dbl, CultureInfo.InvariantCulture);
        if (raw is long l) return l;
        return decimal.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static DateTime ReadUtcDateTime(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return DateTime.UtcNow;
        var raw = reader.GetString(ordinal);
        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? EnsureUtc(parsed)
            : DateTime.UtcNow;
    }

    private static string FormatDecimal(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static string FormatUtc(DateTime dt) => EnsureUtc(dt).ToString("O", CultureInfo.InvariantCulture);

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public ValueTask DisposeAsync()
    {
        _schemaLock.Dispose();
        return ValueTask.CompletedTask;
    }
}

