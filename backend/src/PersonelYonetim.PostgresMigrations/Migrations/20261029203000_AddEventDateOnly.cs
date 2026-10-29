using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.PostgresMigrations.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261029203000_AddEventDateOnly")]
public class AddEventDateOnly : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<bool>(name: "IsDateOnly", schema: "personel_app", table: "Events", type: "boolean", nullable: false, defaultValue: false);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(name: "IsDateOnly", schema: "personel_app", table: "Events");
}
