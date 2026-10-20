using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EtsyMarketPlace.Infrastructure.Database.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "order_addresses",
                columns: table => new
                {
                    receipt_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    buyer_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, defaultValue: ""),
                    buyer_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, defaultValue: ""),
                    phone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    street_address = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    second_address = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    postal_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    country_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: ""),
                    country_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ioss_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    product_cost_usd = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    shipping_cost_usd = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    weight_kg = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    width_cm = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    length_cm = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    height_cm = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    gtip_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    selected_carrier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    carrier_service_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    tracking_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    label_url = table.Column<string>(type: "text", nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_addresses", x => x.receipt_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_addresses_updated_at_utc",
                table: "order_addresses",
                column: "updated_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_addresses");
        }
    }
}
