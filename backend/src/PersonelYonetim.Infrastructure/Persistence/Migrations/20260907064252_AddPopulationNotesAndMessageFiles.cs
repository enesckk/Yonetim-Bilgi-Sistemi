using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonelYonetim.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPopulationNotesAndMessageFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChildCount",
                table: "SettlementPopulations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FemaleCount",
                table: "SettlementPopulations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaleCount",
                table: "SettlementPopulations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentContentType",
                table: "DirectMessages",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentFileName",
                table: "DirectMessages",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentPath",
                table: "DirectMessages",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedEventId",
                table: "DirectMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EventNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventNotes_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventNotes_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_RelatedEventId",
                table: "DirectMessages",
                column: "RelatedEventId");

            migrationBuilder.CreateIndex(
                name: "IX_EventNotes_AuthorUserId",
                table: "EventNotes",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EventNotes_EventId_CreatedAtUtc",
                table: "EventNotes",
                columns: new[] { "EventId", "CreatedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_DirectMessages_Events_RelatedEventId",
                table: "DirectMessages",
                column: "RelatedEventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectMessages_Events_RelatedEventId",
                table: "DirectMessages");

            migrationBuilder.DropTable(
                name: "EventNotes");

            migrationBuilder.DropIndex(
                name: "IX_DirectMessages_RelatedEventId",
                table: "DirectMessages");

            migrationBuilder.DropColumn(
                name: "ChildCount",
                table: "SettlementPopulations");

            migrationBuilder.DropColumn(
                name: "FemaleCount",
                table: "SettlementPopulations");

            migrationBuilder.DropColumn(
                name: "MaleCount",
                table: "SettlementPopulations");

            migrationBuilder.DropColumn(
                name: "AttachmentContentType",
                table: "DirectMessages");

            migrationBuilder.DropColumn(
                name: "AttachmentFileName",
                table: "DirectMessages");

            migrationBuilder.DropColumn(
                name: "AttachmentPath",
                table: "DirectMessages");

            migrationBuilder.DropColumn(
                name: "RelatedEventId",
                table: "DirectMessages");
        }
    }
}
