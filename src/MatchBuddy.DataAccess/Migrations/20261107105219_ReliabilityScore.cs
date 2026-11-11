using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatchBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ReliabilityScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReliabilityScore",
                table: "Players",
                type: "int",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<int>(
                name: "MinReliability",
                table: "Matchs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReliabilitySettledAt",
                table: "Matchs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReliabilityEvents",
                columns: table => new
                {
                    ReliabilityEventId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    Delta = table.Column<int>(type: "int", nullable: false),
                    ReasonCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReversedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversedByPlayerId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReliabilityEvents", x => x.ReliabilityEventId);
                    table.ForeignKey(
                        name: "FK_ReliabilityEvents_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReliabilityEvents_MatchId_PlayerId",
                table: "ReliabilityEvents",
                columns: new[] { "MatchId", "PlayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReliabilityEvents_PlayerId_CreatedAt",
                table: "ReliabilityEvents",
                columns: new[] { "PlayerId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReliabilityEvents");

            migrationBuilder.DropColumn(
                name: "ReliabilityScore",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "MinReliability",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "ReliabilitySettledAt",
                table: "Matchs");
        }
    }
}
