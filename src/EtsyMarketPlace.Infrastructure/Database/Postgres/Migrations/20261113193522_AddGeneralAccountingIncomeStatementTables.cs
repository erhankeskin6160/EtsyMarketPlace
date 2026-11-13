using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EtsyMarketPlace.Infrastructure.Database.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddGeneralAccountingIncomeStatementTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounting_fixed_assets",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    shop_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    asset_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    asset_account_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    expense_target_category = table.Column<int>(type: "integer", nullable: false),
                    acquisition_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    acquisition_cost_try = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    salvage_value_try = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    useful_life_years = table.Column<int>(type: "integer", nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_fixed_assets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "accounting_inventory_adjustments",
                columns: table => new
                {
                    shop_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false),
                    opening_inventory_try = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    gross_purchases_try = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    purchase_returns_and_discounts_try = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    closing_inventory_try = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_inventory_adjustments", x => new { x.shop_id, x.year, x.month });
                });

            migrationBuilder.CreateTable(
                name: "accounting_operating_expenses",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    shop_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entry_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    document_no = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    exchange_rate_to_try = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    amount_try = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    is_tax_deductible = table.Column<bool>(type: "boolean", nullable: false),
                    frequency = table.Column<int>(type: "integer", nullable: false),
                    recurring_end_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_operating_expenses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "accounting_shop_profiles",
                columns: table => new
                {
                    shop_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    commission_target = table.Column<int>(type: "integer", nullable: false),
                    shipping_target = table.Column<int>(type: "integer", nullable: false),
                    canceled_cost_target = table.Column<int>(type: "integer", nullable: false),
                    inventory_method = table.Column<int>(type: "integer", nullable: false),
                    tax_regime = table.Column<int>(type: "integer", nullable: false),
                    young_entrepreneur_mode_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    young_entrepreneur_annual_limit_try = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    young_entrepreneur_prior_used_try = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    e_export_exemption_rate_pct = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    custom_or_corporate_tax_rate_pct = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    custom_gvk_brackets_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    include_fx_spread_and_exchange_difference = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_shop_profiles", x => x.shop_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounting_fixed_assets_shop_id",
                table: "accounting_fixed_assets",
                column: "shop_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_operating_expenses_shop_id_entry_date",
                table: "accounting_operating_expenses",
                columns: new[] { "shop_id", "entry_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounting_fixed_assets");

            migrationBuilder.DropTable(
                name: "accounting_inventory_adjustments");

            migrationBuilder.DropTable(
                name: "accounting_operating_expenses");

            migrationBuilder.DropTable(
                name: "accounting_shop_profiles");
        }
    }
}
