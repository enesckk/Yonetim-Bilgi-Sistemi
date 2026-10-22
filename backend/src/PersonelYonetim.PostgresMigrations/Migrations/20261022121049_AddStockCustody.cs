using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonelYonetim.PostgresMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddStockCustody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "Kind",
                schema: "personel_app",
                table: "StockItems",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.CreateTable(
                name: "StockCustodies",
                schema: "personel_app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StockItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Unit = table.Column<byte>(type: "smallint", nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AssignedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ReturnedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    ReturnNotes = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    ReplacesId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_StockCustodies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockCustodies_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "personel_app",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockCustodies_OrganizationUnits_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "personel_app",
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockCustodies_StockCustodies_ReplacesId",
                        column: x => x.ReplacesId,
                        principalSchema: "personel_app",
                        principalTable: "StockCustodies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockCustodies_StockItems_StockItemId",
                        column: x => x.StockItemId,
                        principalSchema: "personel_app",
                        principalTable: "StockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockCustodies_EmployeeId_ReturnedOn",
                schema: "personel_app",
                table: "StockCustodies",
                columns: new[] { "EmployeeId", "ReturnedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_StockCustodies_LocationId",
                schema: "personel_app",
                table: "StockCustodies",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockCustodies_ReplacesId",
                schema: "personel_app",
                table: "StockCustodies",
                column: "ReplacesId",
                unique: true,
                filter: "\"ReplacesId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockCustodies_StockItemId_LocationId_ReturnedOn",
                schema: "personel_app",
                table: "StockCustodies",
                columns: new[] { "StockItemId", "LocationId", "ReturnedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_StockCustodies_StockItemId_SerialNumber",
                schema: "personel_app",
                table: "StockCustodies",
                columns: new[] { "StockItemId", "SerialNumber" },
                unique: true,
                filter: "\"SerialNumber\" IS NOT NULL AND \"ReturnedOn\" IS NULL AND \"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockCustodies",
                schema: "personel_app");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "personel_app",
                table: "StockItems");
        }
    }
}
