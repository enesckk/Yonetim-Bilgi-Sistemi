using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonelYonetim.Infrastructure.Persistence;

#nullable disable

namespace PersonelYonetim.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260906193000_AddEventResponsibleEmployee")]
public partial class AddEventResponsibleEmployee : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('Events', 'ResponsibleEmployeeId') IS NULL
                ALTER TABLE [Events] ADD [ResponsibleEmployeeId] uniqueidentifier NULL;

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Events_ResponsibleEmployeeId' AND object_id = OBJECT_ID(N'[Events]'))
                CREATE INDEX [IX_Events_ResponsibleEmployeeId] ON [Events] ([ResponsibleEmployeeId]);

            IF NOT EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE name = N'FK_Events_Employees_ResponsibleEmployeeId')
                ALTER TABLE [Events] WITH CHECK ADD CONSTRAINT [FK_Events_Employees_ResponsibleEmployeeId]
                    FOREIGN KEY ([ResponsibleEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE SET NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE name = N'FK_Events_Employees_ResponsibleEmployeeId')
                ALTER TABLE [Events] DROP CONSTRAINT [FK_Events_Employees_ResponsibleEmployeeId];

            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Events_ResponsibleEmployeeId' AND object_id = OBJECT_ID(N'[Events]'))
                DROP INDEX [IX_Events_ResponsibleEmployeeId] ON [Events];

            IF COL_LENGTH('Events', 'ResponsibleEmployeeId') IS NOT NULL
                ALTER TABLE [Events] DROP COLUMN [ResponsibleEmployeeId];
            """);
    }
}
