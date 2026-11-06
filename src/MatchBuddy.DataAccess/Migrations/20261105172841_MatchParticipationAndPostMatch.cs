using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatchBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class MatchParticipationAndPostMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AwayScore",
                table: "Matchs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByPlayerId",
                table: "Matchs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeScore",
                table: "Matchs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResultEnteredAt",
                table: "Matchs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MatchLikes",
                columns: table => new
                {
                    MatchLikeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchLikes", x => x.MatchLikeId);
                    table.ForeignKey(
                        name: "FK_MatchLikes_Matchs_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matchs",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchLikes_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                });

            migrationBuilder.CreateTable(
                name: "MatchParticipants",
                columns: table => new
                {
                    MatchParticipantId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Side = table.Column<byte>(type: "tinyint", nullable: true),
                    Confirmed = table.Column<bool>(type: "bit", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReminderSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Goals = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Assists = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchParticipants", x => x.MatchParticipantId);
                    table.ForeignKey(
                        name: "FK_MatchParticipants_Matchs_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matchs",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchParticipants_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                });

            migrationBuilder.CreateTable(
                name: "MvpVotes",
                columns: table => new
                {
                    MvpVoteId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    VoterPlayerId = table.Column<int>(type: "int", nullable: false),
                    VotedPlayerId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MvpVotes", x => x.MvpVoteId);
                    table.ForeignKey(
                        name: "FK_MvpVotes_Matchs_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matchs",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MvpVotes_Players_VotedPlayerId",
                        column: x => x.VotedPlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                    table.ForeignKey(
                        name: "FK_MvpVotes_Players_VoterPlayerId",
                        column: x => x.VoterPlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                });

            migrationBuilder.CreateTable(
                name: "PlayerRatings",
                columns: table => new
                {
                    PlayerRatingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    RaterPlayerId = table.Column<int>(type: "int", nullable: false),
                    RatedPlayerId = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerRatings", x => x.PlayerRatingId);
                    table.ForeignKey(
                        name: "FK_PlayerRatings_Matchs_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matchs",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerRatings_Players_RatedPlayerId",
                        column: x => x.RatedPlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                    table.ForeignKey(
                        name: "FK_PlayerRatings_Players_RaterPlayerId",
                        column: x => x.RaterPlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matchs_CreatedByPlayerId",
                table: "Matchs",
                column: "CreatedByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchLikes_MatchId_PlayerId",
                table: "MatchLikes",
                columns: new[] { "MatchId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchLikes_PlayerId",
                table: "MatchLikes",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchParticipants_MatchId_PlayerId",
                table: "MatchParticipants",
                columns: new[] { "MatchId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchParticipants_PlayerId",
                table: "MatchParticipants",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_MvpVotes_MatchId_VoterPlayerId",
                table: "MvpVotes",
                columns: new[] { "MatchId", "VoterPlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MvpVotes_VotedPlayerId",
                table: "MvpVotes",
                column: "VotedPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_MvpVotes_VoterPlayerId",
                table: "MvpVotes",
                column: "VoterPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerRatings_MatchId_RaterPlayerId_RatedPlayerId",
                table: "PlayerRatings",
                columns: new[] { "MatchId", "RaterPlayerId", "RatedPlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerRatings_RatedPlayerId",
                table: "PlayerRatings",
                column: "RatedPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerRatings_RaterPlayerId",
                table: "PlayerRatings",
                column: "RaterPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Matchs_Players_CreatedByPlayerId",
                table: "Matchs",
                column: "CreatedByPlayerId",
                principalTable: "Players",
                principalColumn: "PlayerId");

            // Veri taşıma: eski maçların kadrosu MatchTeam + PlayerTeam üzerinden MatchParticipant'a yazılır.
            // Maçın ilk takımı (en küçük MatchTeamId) Side = 1, ikinci takımı Side = 2; 3. ve sonraki takımlar yok sayılır.
            // Status = 1 (Going), Confirmed = 0, JoinedAt = GETDATE(). Kapasite aşılsa da hepsi Going kalır.
            // Oyuncu iki takımda da varsa ilk kayıt (Side 1) tutulur.
            migrationBuilder.Sql(@"
;WITH OrderedTeams AS (
    SELECT mt.MatchId, mt.TeamId,
           ROW_NUMBER() OVER (PARTITION BY mt.MatchId ORDER BY mt.MatchTeamId) AS TeamOrder
    FROM MatchTeam mt
),
Candidates AS (
    SELECT ot.MatchId, pt.PlayerId, CAST(ot.TeamOrder AS tinyint) AS Side,
           ROW_NUMBER() OVER (PARTITION BY ot.MatchId, pt.PlayerId ORDER BY ot.TeamOrder, pt.PlayerTeamId) AS DupOrder
    FROM OrderedTeams ot
    INNER JOIN PlayerTeam pt ON pt.TeamId = ot.TeamId
    WHERE ot.TeamOrder <= 2
)
INSERT INTO MatchParticipants (MatchId, PlayerId, Status, Side, Confirmed, JoinedAt, ReminderSentAt, Goals, Assists)
SELECT c.MatchId, c.PlayerId, 1, c.Side, 0, GETDATE(), NULL, 0, 0
FROM Candidates c
WHERE c.DupOrder = 1
  AND EXISTS (SELECT 1 FROM Matchs m WHERE m.MatchId = c.MatchId)
  AND EXISTS (SELECT 1 FROM Players p WHERE p.PlayerId = c.PlayerId)
  AND NOT EXISTS (SELECT 1 FROM MatchParticipants x WHERE x.MatchId = c.MatchId AND x.PlayerId = c.PlayerId);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matchs_Players_CreatedByPlayerId",
                table: "Matchs");

            migrationBuilder.DropTable(
                name: "MatchLikes");

            migrationBuilder.DropTable(
                name: "MatchParticipants");

            migrationBuilder.DropTable(
                name: "MvpVotes");

            migrationBuilder.DropTable(
                name: "PlayerRatings");

            migrationBuilder.DropIndex(
                name: "IX_Matchs_CreatedByPlayerId",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "AwayScore",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "CreatedByPlayerId",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "HomeScore",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "ResultEnteredAt",
                table: "Matchs");
        }
    }
}
