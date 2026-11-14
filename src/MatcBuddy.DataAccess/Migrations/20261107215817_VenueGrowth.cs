using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatcBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class VenueGrowth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SeriesId",
                table: "SlotBlocks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyDeals",
                table: "PlayerSettings",
                type: "bit",
                nullable: false,
                defaultValue: true); // mevcut oyuncular için fırsat bildirimleri açık

            migrationBuilder.AddColumn<int>(
                name: "SeriesId",
                table: "Matchs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DealNotifiedCount",
                table: "Campaigns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DealSentAt",
                table: "Campaigns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeal",
                table: "Campaigns",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MatchSeries",
                columns: table => new
                {
                    SeriesId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedByPlayerId = table.Column<int>(type: "int", nullable: false),
                    StadiumId = table.Column<int>(type: "int", nullable: false),
                    PitchId = table.Column<int>(type: "int", nullable: false),
                    NextDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Approved = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StoppedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchSeries", x => x.SeriesId);
                    table.ForeignKey(
                        name: "FK_MatchSeries_Pitches_PitchId",
                        column: x => x.PitchId,
                        principalTable: "Pitches",
                        principalColumn: "PitchId");
                    table.ForeignKey(
                        name: "FK_MatchSeries_Players_CreatedByPlayerId",
                        column: x => x.CreatedByPlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matchs_SeriesId",
                table: "Matchs",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchSeries_CreatedByPlayerId",
                table: "MatchSeries",
                column: "CreatedByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchSeries_IsActive_NextDate",
                table: "MatchSeries",
                columns: new[] { "IsActive", "NextDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchSeries_PitchId",
                table: "MatchSeries",
                column: "PitchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchSeries");

            migrationBuilder.DropIndex(
                name: "IX_Matchs_SeriesId",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "SlotBlocks");

            migrationBuilder.DropColumn(
                name: "NotifyDeals",
                table: "PlayerSettings");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "DealNotifiedCount",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "DealSentAt",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "IsDeal",
                table: "Campaigns");
        }
    }
}
