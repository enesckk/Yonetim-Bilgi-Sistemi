using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonelYonetim.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockCustody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "Kind",
                table: "StockItems",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.CreateTable(
                name: "StockCustodies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Unit = table.Column<byte>(type: "tinyint", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AssignedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ReturnedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ReturnNotes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ReplacesId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockCustodies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockCustodies_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockCustodies_OrganizationUnits_LocationId",
                        column: x => x.LocationId,
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockCustodies_StockCustodies_ReplacesId",
                        column: x => x.ReplacesId,
                        principalTable: "StockCustodies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockCustodies_StockItems_StockItemId",
                        column: x => x.StockItemId,
                        principalTable: "StockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockCustodies_EmployeeId_ReturnedOn",
                table: "StockCustodies",
                columns: new[] { "EmployeeId", "ReturnedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_StockCustodies_LocationId",
                table: "StockCustodies",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockCustodies_ReplacesId",
                table: "StockCustodies",
                column: "ReplacesId",
                unique: true,
                filter: "[ReplacesId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockCustodies_StockItemId_LocationId_ReturnedOn",
                table: "StockCustodies",
                columns: new[] { "StockItemId", "LocationId", "ReturnedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_StockCustodies_StockItemId_SerialNumber",
                table: "StockCustodies",
                columns: new[] { "StockItemId", "SerialNumber" },
                unique: true,
                filter: "[SerialNumber] IS NOT NULL AND [ReturnedOn] IS NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockCustodies");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "StockItems");
        }
    }
}
