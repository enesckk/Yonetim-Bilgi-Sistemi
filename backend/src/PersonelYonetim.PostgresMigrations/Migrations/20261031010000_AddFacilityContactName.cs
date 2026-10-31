using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonelYonetim.Infrastructure.Persistence;
namespace PersonelYonetim.PostgresMigrations.Migrations;
[DbContext(typeof(AppDbContext))]
[Migration("20261031010000_AddFacilityContactName")]
public class AddFacilityContactName : Migration
{
    protected override void Up(MigrationBuilder m) => m.AddColumn<string>(name: "ContactName", schema: "personel_app", table: "OrganizationUnits", type: "character varying(120)", maxLength: 120, nullable: true);
    protected override void Down(MigrationBuilder m) => m.DropColumn(name: "ContactName", schema: "personel_app", table: "OrganizationUnits");
}
