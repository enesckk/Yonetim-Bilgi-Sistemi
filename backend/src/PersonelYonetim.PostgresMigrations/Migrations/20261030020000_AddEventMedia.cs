using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.PostgresMigrations.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261030020000_AddEventMedia")]
public class AddEventMedia : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<string>(name: "Requirements", table: "Events", schema: "personel_app", type: "character varying(8000)", maxLength: 8000, nullable: true);
        m.CreateTable(name: "EventMedia", schema: "personel_app", columns: table => new
        {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                FileName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                FileData = table.Column<byte[]>(type: "bytea", nullable: false),
                Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                Size = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_EventMedia", x => x.Id);
            table.ForeignKey(name: "FK_EventMedia_Events_EventId", column: x => x.EventId, principalTable: "Events", principalSchema: "personel_app", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex(name: "IX_EventMedia_EventId", table: "EventMedia", schema: "personel_app", column: "EventId");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable(name: "EventMedia", schema: "personel_app");
        m.DropColumn(name: "Requirements", table: "Events", schema: "personel_app");
    }
}
