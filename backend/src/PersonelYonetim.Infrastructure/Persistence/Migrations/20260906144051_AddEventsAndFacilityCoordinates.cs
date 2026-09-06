using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonelYonetim.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventsAndFacilityCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Önceki yarım kalmış denemelerde kolonlar oluşmuş olabilir.
            migrationBuilder.Sql("""
                IF COL_LENGTH('OrganizationUnits', 'Latitude') IS NULL
                    ALTER TABLE [OrganizationUnits] ADD [Latitude] float NULL;
                IF COL_LENGTH('OrganizationUnits', 'Longitude') IS NULL
                    ALTER TABLE [OrganizationUnits] ADD [Longitude] float NULL;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[Events]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [Events] (
                        [Id] uniqueidentifier NOT NULL,
                        [Title] nvarchar(200) NOT NULL,
                        [Description] nvarchar(4000) NULL,
                        [StartAtUtc] datetime2 NOT NULL,
                        [EndAtUtc] datetime2 NULL,
                        [Status] tinyint NOT NULL,
                        [OrganizingUnitId] uniqueidentifier NULL,
                        [FacilityId] uniqueidentifier NULL,
                        [Latitude] float NULL,
                        [Longitude] float NULL,
                        [Address] nvarchar(500) NULL,
                        [CreatedAtUtc] datetime2 NOT NULL,
                        [CreatedBy] nvarchar(100) NULL,
                        [UpdatedAtUtc] datetime2 NULL,
                        [UpdatedBy] nvarchar(100) NULL,
                        [IsDeleted] bit NOT NULL,
                        [DeletedAtUtc] datetime2 NULL,
                        [DeletedBy] nvarchar(100) NULL,
                        CONSTRAINT [PK_Events] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_Events_OrganizationUnits_FacilityId]
                            FOREIGN KEY ([FacilityId]) REFERENCES [OrganizationUnits] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_Events_OrganizationUnits_OrganizingUnitId]
                            FOREIGN KEY ([OrganizingUnitId]) REFERENCES [OrganizationUnits] ([Id]) ON DELETE NO ACTION
                    );

                    CREATE INDEX [IX_Events_FacilityId] ON [Events] ([FacilityId]);
                    CREATE INDEX [IX_Events_OrganizingUnitId] ON [Events] ([OrganizingUnitId]);
                    CREATE INDEX [IX_Events_StartAtUtc] ON [Events] ([StartAtUtc]);
                    CREATE INDEX [IX_Events_Status] ON [Events] ([Status]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[Events]', N'U') IS NOT NULL
                    DROP TABLE [Events];
                IF COL_LENGTH('OrganizationUnits', 'Latitude') IS NOT NULL
                    ALTER TABLE [OrganizationUnits] DROP COLUMN [Latitude];
                IF COL_LENGTH('OrganizationUnits', 'Longitude') IS NOT NULL
                    ALTER TABLE [OrganizationUnits] DROP COLUMN [Longitude];
                """);
        }
    }
}
