using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EtsyMarketPlace.Infrastructure.Database.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase4ListingsBatchAiTrackingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_usage_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    module_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    model_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    prompt_tokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    completion_tokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    total_tokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cost_usd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false, defaultValue: 0m),
                    cost_try = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false, defaultValue: 0m),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Başarılı"),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_usage_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "batch_queue_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    listing_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: ""),
                    original_title = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    original_description = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    original_tags = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    target_keyword = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, defaultValue: ""),
                    category = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, defaultValue: ""),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    overall_score = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    optimized_title = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    optimized_description = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    optimized_tags = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    optimized_materials = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    risk_warnings = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    issues = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    is_synced_to_etsy = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ab_test_experiment_id = table.Column<long>(type: "bigint", nullable: true),
                    ab_test_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batch_queue_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "listing_ab_tests",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    listing_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: ""),
                    listing_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, defaultValue: ""),
                    experiment_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, defaultValue: ""),
                    variant_a_title = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    variant_b_title = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    variant_a_tags = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    variant_b_tags = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    variant_a_description = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    variant_b_description = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Active"),
                    before_views = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    before_favorites = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    before_sales = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    after_views = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    after_favorites = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    after_sales = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listing_ab_tests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tracking_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_type = table.Column<int>(type: "integer", nullable: false),
                    external_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    display_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    url = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracking_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tracking_snapshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tracking_item_id = table.Column<long>(type: "bigint", nullable: false),
                    captured_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    currency_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: ""),
                    favorites = table.Column<int>(type: "integer", nullable: true),
                    views = table.Column<int>(type: "integer", nullable: true),
                    shop_sales = table.Column<int>(type: "integer", nullable: true),
                    review_count = table.Column<int>(type: "integer", nullable: true),
                    review_average = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    seo_score = table.Column<int>(type: "integer", nullable: true),
                    market_score = table.Column<int>(type: "integer", nullable: true),
                    demand_score = table.Column<int>(type: "integer", nullable: true),
                    competition_score = table.Column<int>(type: "integer", nullable: true),
                    opportunity_score = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracking_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_tracking_snapshots_tracking_items_tracking_item_id",
                        column: x => x.tracking_item_id,
                        principalTable: "tracking_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_history_created_at",
                table: "ai_usage_history",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_history_provider",
                table: "ai_usage_history",
                column: "provider");

            migrationBuilder.CreateIndex(
                name: "IX_batch_queue_items_status",
                table: "batch_queue_items",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_listing_ab_tests_created_at",
                table: "listing_ab_tests",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_listing_ab_tests_listing_id",
                table: "listing_ab_tests",
                column: "listing_id");

            migrationBuilder.CreateIndex(
                name: "IX_tracking_items_entity_type_external_key",
                table: "tracking_items",
                columns: new[] { "entity_type", "external_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tracking_snapshots_tracking_item_id",
                table: "tracking_snapshots",
                column: "tracking_item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_usage_history");

            migrationBuilder.DropTable(
                name: "batch_queue_items");

            migrationBuilder.DropTable(
                name: "listing_ab_tests");

            migrationBuilder.DropTable(
                name: "tracking_snapshots");

            migrationBuilder.DropTable(
                name: "tracking_items");
        }
    }
}
