using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonelYonetim.Infrastructure.Persistence;
namespace PersonelYonetim.Infrastructure.Persistence.Migrations;
[DbContext(typeof(AppDbContext))]
[Migration("20261030030000_AddActivityReports")]
public class AddActivityReports : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<bool>(name: "IncludeInReport", table: "EventMedia", type: "bit", nullable: false, defaultValue: false);
        m.CreateTable(name: "ActivityReports", columns: table => new
        {
            Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
            CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
            UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
            UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
            IsDeleted = table.Column<bool>(type: "bit", nullable: false),
            DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
            DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
            Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
            FromDate = table.Column<DateOnly>(type: "date", nullable: false),
            ToDate = table.Column<DateOnly>(type: "date", nullable: false),
            DateBasis = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
            Kind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
            Revision = table.Column<int>(type: "int", nullable: false),
            SourceCount = table.Column<int>(type: "int", nullable: false),
            SourceAttendance = table.Column<long>(type: "bigint", nullable: false),
            BodyJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
        }, constraints: table => table.PrimaryKey("PK_ActivityReports", x => x.Id));
        m.CreateTable(name: "ActivityReportAssets", columns: table => new
        {
            Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
            CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
            UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
            UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
            IsDeleted = table.Column<bool>(type: "bit", nullable: false),
            DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
            DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
            ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            SourceMediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
            FileName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
            ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
            FileData = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
        }, constraints: table => {
            table.PrimaryKey("PK_ActivityReportAssets", x => x.Id);
            table.ForeignKey(name: "FK_ActivityReportAssets_ActivityReports_ReportId", column: x => x.ReportId, principalTable: "ActivityReports", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex(name: "IX_ActivityReportAssets_ReportId", table: "ActivityReportAssets", column: "ReportId");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable(name: "ActivityReportAssets", schema: null);
        m.DropTable(name: "ActivityReports", schema: null);
        m.DropColumn(name: "IncludeInReport", table: "EventMedia", schema: null);
    }
}
