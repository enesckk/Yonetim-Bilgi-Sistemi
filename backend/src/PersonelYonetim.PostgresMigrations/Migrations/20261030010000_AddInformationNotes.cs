using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.PostgresMigrations.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261030010000_AddInformationNotes")]
public class AddInformationNotes : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable(name: "InformationNotes", schema: "personel_app", columns: table => new
        {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                EventId = table.Column<Guid>(type: "uuid", nullable: true),
                DateText = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Organizer = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                PreparedBy = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Body = table.Column<string>(type: "character varying(30000)", maxLength: 30000, nullable: false),
                FileName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                FileData = table.Column<byte[]>(type: "bytea", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_InformationNotes", x => x.Id);
            table.ForeignKey(name: "FK_InformationNotes_Events_EventId", column: x => x.EventId, principalTable: "Events", principalSchema: "personel_app", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex(name: "IX_InformationNotes_EventId", table: "InformationNotes", schema: "personel_app", column: "EventId");
        m.CreateIndex(name: "IX_InformationNotes_CreatedAtUtc", table: "InformationNotes", schema: "personel_app", column: "CreatedAtUtc");
    }
    protected override void Down(MigrationBuilder m) => m.DropTable(name: "InformationNotes", schema: "personel_app");
}
