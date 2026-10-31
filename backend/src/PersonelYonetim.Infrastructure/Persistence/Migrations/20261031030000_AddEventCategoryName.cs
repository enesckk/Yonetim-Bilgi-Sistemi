using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonelYonetim.Infrastructure.Persistence;
namespace PersonelYonetim.Infrastructure.Persistence.Migrations;
[DbContext(typeof(AppDbContext))]
[Migration("20261031030000_AddEventCategoryName")]
public class AddEventCategoryName : Migration
{
    protected override void Up(MigrationBuilder m) => m.AddColumn<string>(name: "CategoryName", table: "Events", type: "nvarchar(120)", maxLength: 120, nullable: true);
    protected override void Down(MigrationBuilder m) => m.DropColumn(name: "CategoryName", table: "Events");
}
