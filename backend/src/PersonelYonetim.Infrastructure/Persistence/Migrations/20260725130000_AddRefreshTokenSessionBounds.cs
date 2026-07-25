using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonelYonetim.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenSessionBounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AbsoluteExpiresAtUtc",
                table: "RefreshTokens",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsedAtUtc",
                table: "RefreshTokens",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            // Mevcut jetonlar: absolute = ExpiresAtUtc, lastUsed = CreatedAtUtc
            migrationBuilder.Sql("""
                UPDATE RefreshTokens
                SET AbsoluteExpiresAtUtc = ExpiresAtUtc,
                    LastUsedAtUtc = CreatedAtUtc;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbsoluteExpiresAtUtc",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "LastUsedAtUtc",
                table: "RefreshTokens");
        }
    }
}

