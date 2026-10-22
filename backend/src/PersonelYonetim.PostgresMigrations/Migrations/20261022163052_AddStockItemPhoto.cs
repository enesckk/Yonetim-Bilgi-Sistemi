using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonelYonetim.PostgresMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddStockItemPhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoPath",
                schema: "personel_app",
                table: "StockItems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoPath",
                schema: "personel_app",
                table: "StockItems");
        }
    }
}
