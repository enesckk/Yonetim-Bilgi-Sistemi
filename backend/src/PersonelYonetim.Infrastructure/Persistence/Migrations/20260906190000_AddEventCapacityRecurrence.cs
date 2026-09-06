using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonelYonetim.Infrastructure.Persistence;

#nullable disable

namespace PersonelYonetim.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260906190000_AddEventCapacityRecurrence")]
public partial class AddEventCapacityRecurrence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('Events', 'ExpectedAttendees') IS NULL
                ALTER TABLE [Events] ADD [ExpectedAttendees] int NULL;
            IF COL_LENGTH('Events', 'SeriesId') IS NULL
                ALTER TABLE [Events] ADD [SeriesId] uniqueidentifier NULL;
            IF COL_LENGTH('Events', 'RecurrenceFrequency') IS NULL
                ALTER TABLE [Events] ADD [RecurrenceFrequency] tinyint NOT NULL CONSTRAINT [DF_Events_RecurrenceFrequency] DEFAULT 0;

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Events_SeriesId' AND object_id = OBJECT_ID(N'[Events]'))
                CREATE INDEX [IX_Events_SeriesId] ON [Events] ([SeriesId]);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Events_SeriesId' AND object_id = OBJECT_ID(N'[Events]'))
                DROP INDEX [IX_Events_SeriesId] ON [Events];

            IF COL_LENGTH('Events', 'RecurrenceFrequency') IS NOT NULL
            BEGIN
                DECLARE @df sysname;
                SELECT @df = dc.name
                FROM sys.default_constraints dc
                JOIN sys.columns c ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'[Events]') AND c.name = N'RecurrenceFrequency';
                IF @df IS NOT NULL EXEC(N'ALTER TABLE [Events] DROP CONSTRAINT [' + @df + N']');
                ALTER TABLE [Events] DROP COLUMN [RecurrenceFrequency];
            END
            IF COL_LENGTH('Events', 'SeriesId') IS NOT NULL
                ALTER TABLE [Events] DROP COLUMN [SeriesId];
            IF COL_LENGTH('Events', 'ExpectedAttendees') IS NOT NULL
                ALTER TABLE [Events] DROP COLUMN [ExpectedAttendees];
            """);
    }
}
