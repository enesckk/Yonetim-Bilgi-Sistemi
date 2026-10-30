using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonelYonetim.Infrastructure.Persistence;
namespace PersonelYonetim.PostgresMigrations.Migrations;
[DbContext(typeof(AppDbContext))]
[Migration("20261030030000_AddActivityReports")]
public class AddActivityReports : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<bool>(name: "IncludeInReport", table: "EventMedia", schema: "personel_app", type: "boolean", nullable: false, defaultValue: false);
        m.CreateTable(name: "ActivityReports", schema: "personel_app", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
            UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
            IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
            DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
            Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
            FromDate = table.Column<DateOnly>(type: "date", nullable: false),
            ToDate = table.Column<DateOnly>(type: "date", nullable: false),
            DateBasis = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
            Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
            Revision = table.Column<int>(type: "integer", nullable: false),
            SourceCount = table.Column<int>(type: "integer", nullable: false),
            SourceAttendance = table.Column<long>(type: "bigint", nullable: false),
            BodyJson = table.Column<string>(type: "text", nullable: false),
        }, constraints: table => table.PrimaryKey("PK_ActivityReports", x => x.Id));
        m.CreateTable(name: "ActivityReportAssets", schema: "personel_app", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
            UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
            IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
            DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
            ReportId = table.Column<Guid>(type: "uuid", nullable: false),
            SourceMediaId = table.Column<Guid>(type: "uuid", nullable: true),
            FileName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
            ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            FileData = table.Column<byte[]>(type: "bytea", nullable: false),
        }, constraints: table => {
            table.PrimaryKey("PK_ActivityReportAssets", x => x.Id);
            table.ForeignKey(name: "FK_ActivityReportAssets_ActivityReports_ReportId", column: x => x.ReportId, principalSchema: "personel_app", principalTable: "ActivityReports", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex(name: "IX_ActivityReportAssets_ReportId", table: "ActivityReportAssets", schema: "personel_app", column: "ReportId");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable(name: "ActivityReportAssets", schema: "personel_app");
        m.DropTable(name: "ActivityReports", schema: "personel_app");
        m.DropColumn(name: "IncludeInReport", table: "EventMedia", schema: "personel_app");
    }
}
