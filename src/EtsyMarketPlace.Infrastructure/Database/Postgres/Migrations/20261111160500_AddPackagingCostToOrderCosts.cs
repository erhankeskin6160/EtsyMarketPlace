using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EtsyMarketPlace.Infrastructure.Database.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddPackagingCostToOrderCosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "packaging_cost",
                table: "order_costs",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "packaging_cost",
                table: "order_costs");
        }
    }
}
