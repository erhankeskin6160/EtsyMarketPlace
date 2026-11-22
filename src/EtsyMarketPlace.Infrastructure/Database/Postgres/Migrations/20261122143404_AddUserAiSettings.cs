using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EtsyMarketPlace.Infrastructure.Database.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAiSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_ai_settings",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "text", nullable: false),
                    encrypted_settings_json = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    active_provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Gemini"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_ai_settings", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_user_ai_settings_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_ai_settings");
        }
    }
}
