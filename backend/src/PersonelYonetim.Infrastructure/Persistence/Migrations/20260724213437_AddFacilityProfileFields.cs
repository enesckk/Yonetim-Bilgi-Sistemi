using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonelYonetim.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFacilityProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "OrganizationUnits",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "OrganizationUnits",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FacilityCategoryId",
                table: "OrganizationUnits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkingHours",
                table: "OrganizationUnits",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FacilityCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_FacilityCategories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationUnits_FacilityCategoryId",
                table: "OrganizationUnits",
                column: "FacilityCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityCategories_Code",
                table: "FacilityCategories",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationUnits_FacilityCategories_FacilityCategoryId",
                table: "OrganizationUnits",
                column: "FacilityCategoryId",
                principalTable: "FacilityCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationUnits_FacilityCategories_FacilityCategoryId",
                table: "OrganizationUnits");

            migrationBuilder.DropTable(
                name: "FacilityCategories");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationUnits_FacilityCategoryId",
                table: "OrganizationUnits");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "OrganizationUnits");

            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "OrganizationUnits");

            migrationBuilder.DropColumn(
                name: "FacilityCategoryId",
                table: "OrganizationUnits");

            migrationBuilder.DropColumn(
                name: "WorkingHours",
                table: "OrganizationUnits");
        }
    }
}
