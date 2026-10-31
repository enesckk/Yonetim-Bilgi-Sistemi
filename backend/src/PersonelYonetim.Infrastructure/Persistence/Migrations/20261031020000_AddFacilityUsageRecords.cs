using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonelYonetim.Infrastructure.Persistence;
namespace PersonelYonetim.Infrastructure.Persistence.Migrations;
[DbContext(typeof(AppDbContext))]
[Migration("20261031020000_AddFacilityUsageRecords")]
public class AddFacilityUsageRecords : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable(name: "FacilityUsageRecords", columns: t => new {
            Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
            FacilityId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
            Year = t.Column<int>(type: "int", nullable: false),
            Month = t.Column<int>(type: "int", nullable: false),
            ParticipantCount = t.Column<long>(type: "bigint", nullable: false),
            SourceKey = t.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
            SourceReference = t.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
            CreatedAtUtc = t.Column<DateTime>(type: "datetime2", nullable: false),
            CreatedBy = t.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
            UpdatedAtUtc = t.Column<DateTime>(type: "datetime2", nullable: true),
            UpdatedBy = t.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
            IsDeleted = t.Column<bool>(type: "bit", nullable: false),
            DeletedAtUtc = t.Column<DateTime>(type: "datetime2", nullable: true),
            DeletedBy = t.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
        }, constraints: t => {
            t.PrimaryKey("PK_FacilityUsageRecords", x => x.Id);
            t.ForeignKey(name: "FK_FacilityUsageRecords_OrganizationUnits_FacilityId", column: x => x.FacilityId, principalTable: "OrganizationUnits", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex(name: "IX_FacilityUsageRecords_SourceKey", table: "FacilityUsageRecords", column: "SourceKey", unique: true);
        m.CreateIndex(name: "IX_FacilityUsageRecords_FacilityId", table: "FacilityUsageRecords", column: "FacilityId");
        m.CreateIndex(name: "IX_FacilityUsageRecords_Year_Month", table: "FacilityUsageRecords", columns: new[] { "Year", "Month" });
    }
    protected override void Down(MigrationBuilder m) => m.DropTable(name: "FacilityUsageRecords", schema: null);
}
