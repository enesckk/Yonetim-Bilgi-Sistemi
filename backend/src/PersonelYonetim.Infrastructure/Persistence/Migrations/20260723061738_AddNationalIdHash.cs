using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonelYonetim.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNationalIdHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NationalIdHash",
                table: "EmployeeSensitiveData",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSensitiveData_NationalIdHash",
                table: "EmployeeSensitiveData",
                column: "NationalIdHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmployeeSensitiveData_NationalIdHash",
                table: "EmployeeSensitiveData");

            migrationBuilder.DropColumn(
                name: "NationalIdHash",
                table: "EmployeeSensitiveData");
        }
    }
}
