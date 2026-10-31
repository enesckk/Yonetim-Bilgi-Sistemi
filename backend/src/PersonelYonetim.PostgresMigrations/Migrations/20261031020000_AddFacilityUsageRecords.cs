using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonelYonetim.Infrastructure.Persistence;
namespace PersonelYonetim.PostgresMigrations.Migrations;
[DbContext(typeof(AppDbContext))]
[Migration("20261031020000_AddFacilityUsageRecords")]
public class AddFacilityUsageRecords : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable(name: "FacilityUsageRecords", schema: "personel_app", columns: t => new {
            Id = t.Column<Guid>(type: "uuid", nullable: false),
            FacilityId = t.Column<Guid>(type: "uuid", nullable: false),
            Year = t.Column<int>(type: "integer", nullable: false),
            Month = t.Column<int>(type: "integer", nullable: false),
            ParticipantCount = t.Column<long>(type: "bigint", nullable: false),
            SourceKey = t.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            SourceReference = t.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
            CreatedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            CreatedBy = t.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
            UpdatedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            UpdatedBy = t.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
            IsDeleted = t.Column<bool>(type: "boolean", nullable: false),
            DeletedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            DeletedBy = t.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
        }, constraints: t => {
            t.PrimaryKey("PK_FacilityUsageRecords", x => x.Id);
            t.ForeignKey(name: "FK_FacilityUsageRecords_OrganizationUnits_FacilityId", column: x => x.FacilityId, principalSchema: "personel_app", principalTable: "OrganizationUnits", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex(name: "IX_FacilityUsageRecords_SourceKey", schema: "personel_app", table: "FacilityUsageRecords", column: "SourceKey", unique: true);
        m.CreateIndex(name: "IX_FacilityUsageRecords_FacilityId", schema: "personel_app", table: "FacilityUsageRecords", column: "FacilityId");
        m.CreateIndex(name: "IX_FacilityUsageRecords_Year_Month", schema: "personel_app", table: "FacilityUsageRecords", columns: new[] { "Year", "Month" });
    }
    protected override void Down(MigrationBuilder m) => m.DropTable(name: "FacilityUsageRecords", schema: "personel_app");
}
