using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EtsyMarketPlace.Infrastructure.Database.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgresCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_users",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    password_salt = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "StoreOwner"),
                    assigned_shop_ids = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    monthly_ai_token_quota = table.Column<int>(type: "integer", nullable: false, defaultValue: 500000),
                    used_ai_tokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    details = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shop_connections",
                columns: table => new
                {
                    shop_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    shop_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, defaultValue: ""),
                    user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, defaultValue: ""),
                    last_synchronized_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_connections", x => x.shop_id);
                });

            migrationBuilder.CreateTable(
                name: "bank_payouts",
                columns: table => new
                {
                    shop_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reference_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "USD"),
                    exchange_rate_to_try = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_payouts", x => new { x.shop_id, x.reference_id });
                    table.ForeignKey(
                        name: "FK_bank_payouts_shop_connections_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shop_connections",
                        principalColumn: "shop_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chart_snapshots",
                columns: table => new
                {
                    shop_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    chart_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    period_start = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    period_end = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    image_png_base64 = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chart_snapshots", x => new { x.shop_id, x.chart_type });
                    table.ForeignKey(
                        name: "FK_chart_snapshots_shop_connections_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shop_connections",
                        principalColumn: "shop_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "etsy_app_credentials",
                columns: table => new
                {
                    shop_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    encrypted_keystring = table.Column<string>(type: "text", nullable: false),
                    encrypted_shared_secret = table.Column<string>(type: "text", nullable: false),
                    redirect_uri = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_etsy_app_credentials", x => x.shop_id);
                    table.ForeignKey(
                        name: "FK_etsy_app_credentials_shop_connections_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shop_connections",
                        principalColumn: "shop_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "financial_transactions",
                columns: table => new
                {
                    shop_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reference_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    gross_sales = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    platform_fees = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    internal_ads_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    external_ads_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    product_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    shipping_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    refunds = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "USD")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_transactions", x => new { x.shop_id, x.reference_id });
                    table.ForeignKey(
                        name: "FK_financial_transactions_shop_connections_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shop_connections",
                        principalColumn: "shop_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "listing_ai_audits",
                columns: table => new
                {
                    shop_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    listing_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    current_seo_score = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    optimized_seo_score = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    model = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    result_json = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    audited_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listing_ai_audits", x => new { x.shop_id, x.listing_id });
                    table.ForeignKey(
                        name: "FK_listing_ai_audits_shop_connections_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shop_connections",
                        principalColumn: "shop_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "listing_traffic_daily",
                columns: table => new
                {
                    shop_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    listing_id = table.Column<long>(type: "bigint", nullable: false),
                    snapshot_date = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    views = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    favorites = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    views_today = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    favorites_today = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    units_sold_month = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    revenue_month = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    conversion_rate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false, defaultValue: 0m),
                    image_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    listing_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listing_traffic_daily", x => new { x.shop_id, x.listing_id, x.snapshot_date });
                    table.ForeignKey(
                        name: "FK_listing_traffic_daily_shop_connections_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shop_connections",
                        principalColumn: "shop_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "monthly_order_summaries",
                columns: table => new
                {
                    shop_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    year_month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    order_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    units_sold = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    gross_revenue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    avg_order_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    shipped_orders = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    unfulfilled_orders = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "USD")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monthly_order_summaries", x => new { x.shop_id, x.year_month });
                    table.ForeignKey(
                        name: "FK_monthly_order_summaries_shop_connections_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shop_connections",
                        principalColumn: "shop_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "oauth_tokens",
                columns: table => new
                {
                    shop_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    access_token = table.Column<string>(type: "text", nullable: false),
                    refresh_token = table.Column<string>(type: "text", nullable: false),
                    access_token_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    token_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Bearer")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oauth_tokens", x => x.shop_id);
                    table.ForeignKey(
                        name: "FK_oauth_tokens_shop_connections_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shop_connections",
                        principalColumn: "shop_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_costs",
                columns: table => new
                {
                    shop_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "USD"),
                    order_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    product_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    shipping_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    alert_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_costs", x => new { x.shop_id, x.order_id });
                    table.ForeignKey(
                        name: "FK_order_costs_shop_connections_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shop_connections",
                        principalColumn: "shop_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shop_ai_settings",
                columns: table => new
                {
                    shop_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    settings_json = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_ai_settings", x => x.shop_id);
                    table.ForeignKey(
                        name: "FK_shop_ai_settings_shop_connections_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shop_connections",
                        principalColumn: "shop_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shop_carrier_sessions",
                columns: table => new
                {
                    shop_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    carrier_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    encrypted_credentials = table.Column<string>(type: "text", nullable: false),
                    account_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    service_level = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_connected = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_carrier_sessions", x => new { x.shop_id, x.carrier_id });
                    table.ForeignKey(
                        name: "FK_shop_carrier_sessions_shop_connections_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shop_connections",
                        principalColumn: "shop_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shop_telegram_settings",
                columns: table => new
                {
                    shop_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    encrypted_bot_token = table.Column<string>(type: "text", nullable: false),
                    chat_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    notify_on_orders = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    notify_on_stock = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    daily_brief_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_telegram_settings", x => x.shop_id);
                    table.ForeignKey(
                        name: "FK_shop_telegram_settings_shop_connections_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shop_connections",
                        principalColumn: "shop_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sync_states",
                columns: table => new
                {
                    shop_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    data_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_successful_sync_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_cursor_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_states", x => new { x.shop_id, x.data_type });
                    table.ForeignKey(
                        name: "FK_sync_states_shop_connections_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shop_connections",
                        principalColumn: "shop_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_app_users_email",
                table: "app_users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_app_users_username",
                table: "app_users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_timestamp",
                table: "audit_logs",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_user_id",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_bank_payouts_shop_id_occurred_at",
                table: "bank_payouts",
                columns: new[] { "shop_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_shop_id_occurred_at",
                table: "financial_transactions",
                columns: new[] { "shop_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_order_costs_shop_id_created_at",
                table: "order_costs",
                columns: new[] { "shop_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_users");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "bank_payouts");

            migrationBuilder.DropTable(
                name: "chart_snapshots");

            migrationBuilder.DropTable(
                name: "etsy_app_credentials");

            migrationBuilder.DropTable(
                name: "financial_transactions");

            migrationBuilder.DropTable(
                name: "listing_ai_audits");

            migrationBuilder.DropTable(
                name: "listing_traffic_daily");

            migrationBuilder.DropTable(
                name: "monthly_order_summaries");

            migrationBuilder.DropTable(
                name: "oauth_tokens");

            migrationBuilder.DropTable(
                name: "order_costs");

            migrationBuilder.DropTable(
                name: "shop_ai_settings");

            migrationBuilder.DropTable(
                name: "shop_carrier_sessions");

            migrationBuilder.DropTable(
                name: "shop_telegram_settings");

            migrationBuilder.DropTable(
                name: "sync_states");

            migrationBuilder.DropTable(
                name: "shop_connections");
        }
    }
}
