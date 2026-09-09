using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonelYonetim.Infrastructure.Persistence;

#nullable disable

namespace PersonelYonetim.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260909120000_AddEventActualAttendees")]
public partial class AddEventActualAttendees : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('Events', 'ActualAttendees') IS NULL
                ALTER TABLE [Events] ADD [ActualAttendees] int NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('Events', 'ActualAttendees') IS NOT NULL
                ALTER TABLE [Events] DROP COLUMN [ActualAttendees];
            """);
    }
}
