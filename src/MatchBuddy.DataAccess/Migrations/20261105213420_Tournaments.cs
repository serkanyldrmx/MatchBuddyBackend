using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatchBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Tournaments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    TournamentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Format = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    MaxTeams = table.Column<int>(type: "int", nullable: false),
                    StadiumId = table.Column<int>(type: "int", nullable: true),
                    CreatedByPlayerId = table.Column<int>(type: "int", nullable: false),
                    WinnerTeamId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.TournamentId);
                    table.ForeignKey(
                        name: "FK_Tournaments_Players_CreatedByPlayerId",
                        column: x => x.CreatedByPlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                    table.ForeignKey(
                        name: "FK_Tournaments_Stadiums_StadiumId",
                        column: x => x.StadiumId,
                        principalTable: "Stadiums",
                        principalColumn: "StadiumId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Tournaments_Teams_WinnerTeamId",
                        column: x => x.WinnerTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId");
                });

            migrationBuilder.CreateTable(
                name: "TournamentFixtures",
                columns: table => new
                {
                    FixtureId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    Round = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    HomeTeamId = table.Column<int>(type: "int", nullable: true),
                    AwayTeamId = table.Column<int>(type: "int", nullable: true),
                    MatchDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    HomeScore = table.Column<int>(type: "int", nullable: true),
                    AwayScore = table.Column<int>(type: "int", nullable: true),
                    WinnerTeamId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentFixtures", x => x.FixtureId);
                    table.ForeignKey(
                        name: "FK_TournamentFixtures_Teams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId");
                    table.ForeignKey(
                        name: "FK_TournamentFixtures_Teams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId");
                    table.ForeignKey(
                        name: "FK_TournamentFixtures_Teams_WinnerTeamId",
                        column: x => x.WinnerTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId");
                    table.ForeignKey(
                        name: "FK_TournamentFixtures_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "TournamentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TournamentTeams",
                columns: table => new
                {
                    TournamentTeamId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentTeams", x => x.TournamentTeamId);
                    table.ForeignKey(
                        name: "FK_TournamentTeams_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId");
                    table.ForeignKey(
                        name: "FK_TournamentTeams_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "TournamentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentFixtures_AwayTeamId",
                table: "TournamentFixtures",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentFixtures_HomeTeamId",
                table: "TournamentFixtures",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentFixtures_TournamentId_Round_Position",
                table: "TournamentFixtures",
                columns: new[] { "TournamentId", "Round", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentFixtures_WinnerTeamId",
                table: "TournamentFixtures",
                column: "WinnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_CreatedByPlayerId",
                table: "Tournaments",
                column: "CreatedByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_StadiumId",
                table: "Tournaments",
                column: "StadiumId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_Status_StartDate",
                table: "Tournaments",
                columns: new[] { "Status", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_WinnerTeamId",
                table: "Tournaments",
                column: "WinnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentTeams_TeamId",
                table: "TournamentTeams",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentTeams_TournamentId_TeamId",
                table: "TournamentTeams",
                columns: new[] { "TournamentId", "TeamId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TournamentFixtures");

            migrationBuilder.DropTable(
                name: "TournamentTeams");

            migrationBuilder.DropTable(
                name: "Tournaments");
        }
    }
}
