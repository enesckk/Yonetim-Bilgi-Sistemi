using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonelYonetim.PostgresMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddPartialCustodyReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ReturnedQuantity",
                schema: "personel_app",
                table: "StockCustodies",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Preserve quantities of existing closed records.
            migrationBuilder.Sql("UPDATE personel_app.\"StockCustodies\" SET \"ReturnedQuantity\" = \"Quantity\" WHERE \"ReturnedOn\" IS NOT NULL;");

            migrationBuilder.CreateTable(
                name: "StockCustodyReturns",
                schema: "personel_app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StockCustodyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReturnedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockCustodyReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockCustodyReturns_StockCustodies_StockCustodyId",
                        column: x => x.StockCustodyId,
                        principalSchema: "personel_app",
                        principalTable: "StockCustodies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockCustodyReturns_StockCustodyId",
                schema: "personel_app",
                table: "StockCustodyReturns",
                column: "StockCustodyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockCustodyReturns",
                schema: "personel_app");

            migrationBuilder.DropColumn(
                name: "ReturnedQuantity",
                schema: "personel_app",
                table: "StockCustodies");
        }
    }
}
