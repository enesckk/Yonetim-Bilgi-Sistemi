using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.PostgresMigrations.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261029195500_AddEventParticipantNames")]
public class AddEventParticipantNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(name: "ParticipantNamesJson", schema: "personel_app", table: "Events", type: "text", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "ParticipantNamesJson", schema: "personel_app", table: "Events");
}
