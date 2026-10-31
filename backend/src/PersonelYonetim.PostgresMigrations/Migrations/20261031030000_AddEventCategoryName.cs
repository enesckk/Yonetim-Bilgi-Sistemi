using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonelYonetim.Infrastructure.Persistence;
namespace PersonelYonetim.PostgresMigrations.Migrations;
[DbContext(typeof(AppDbContext))]
[Migration("20261031030000_AddEventCategoryName")]
public class AddEventCategoryName : Migration
{
    protected override void Up(MigrationBuilder m) => m.AddColumn<string>(name: "CategoryName", schema: "personel_app", table: "Events", type: "character varying(120)", maxLength: 120, nullable: true);
    protected override void Down(MigrationBuilder m) => m.DropColumn(name: "CategoryName", schema: "personel_app", table: "Events");
}
