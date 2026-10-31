using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonelYonetim.Infrastructure.Persistence;
namespace PersonelYonetim.Infrastructure.Persistence.Migrations;
[DbContext(typeof(AppDbContext))]
[Migration("20261031010000_AddFacilityContactName")]
public class AddFacilityContactName : Migration
{
    protected override void Up(MigrationBuilder m) => m.AddColumn<string>(name: "ContactName", table: "OrganizationUnits", type: "nvarchar(120)", maxLength: 120, nullable: true);
    protected override void Down(MigrationBuilder m) => m.DropColumn(name: "ContactName", table: "OrganizationUnits");
}
