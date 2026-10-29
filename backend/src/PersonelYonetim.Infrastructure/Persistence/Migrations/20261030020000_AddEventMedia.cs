using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261030020000_AddEventMedia")]
public class AddEventMedia : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<string>(name: "Requirements", table: "Events", type: "nvarchar(max)", maxLength: 8000, nullable: true);
        m.CreateTable(name: "EventMedia", columns: table => new
        {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FileName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                FileData = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                Kind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                Size = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_EventMedia", x => x.Id);
            table.ForeignKey(name: "FK_EventMedia_Events_EventId", column: x => x.EventId, principalTable: "Events", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex(name: "IX_EventMedia_EventId", table: "EventMedia", column: "EventId");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable(name: "EventMedia", schema: null);
        m.DropColumn(name: "Requirements", table: "Events", schema: null);
    }
}
