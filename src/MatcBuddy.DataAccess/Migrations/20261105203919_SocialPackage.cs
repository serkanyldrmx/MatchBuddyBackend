using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatcBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class SocialPackage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchComments_Matchs_MatchId",
                table: "MatchComments");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Players",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Players",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    ActivityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorPlayerId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: true),
                    TeamId = table.Column<int>(type: "int", nullable: true),
                    TargetPlayerId = table.Column<int>(type: "int", nullable: true),
                    Value = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.ActivityId);
                    table.ForeignKey(
                        name: "FK_Activities_Matchs_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matchs",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Activities_Players_ActorPlayerId",
                        column: x => x.ActorPlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                    table.ForeignKey(
                        name: "FK_Activities_Players_TargetPlayerId",
                        column: x => x.TargetPlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                    table.ForeignKey(
                        name: "FK_Activities_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Blocks",
                columns: table => new
                {
                    BlockId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BlockerId = table.Column<int>(type: "int", nullable: false),
                    BlockedId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blocks", x => x.BlockId);
                    table.ForeignKey(
                        name: "FK_Blocks_Players_BlockedId",
                        column: x => x.BlockedId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                    table.ForeignKey(
                        name: "FK_Blocks_Players_BlockerId",
                        column: x => x.BlockerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                });

            migrationBuilder.CreateTable(
                name: "Follows",
                columns: table => new
                {
                    FollowId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FollowerId = table.Column<int>(type: "int", nullable: false),
                    FolloweeId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Follows", x => x.FollowId);
                    table.ForeignKey(
                        name: "FK_Follows_Players_FolloweeId",
                        column: x => x.FolloweeId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                    table.ForeignKey(
                        name: "FK_Follows_Players_FollowerId",
                        column: x => x.FollowerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                });

            migrationBuilder.CreateTable(
                name: "MatchInvites",
                columns: table => new
                {
                    InviteId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    InviterId = table.Column<int>(type: "int", nullable: false),
                    InviteeId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchInvites", x => x.InviteId);
                    table.ForeignKey(
                        name: "FK_MatchInvites_Matchs_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matchs",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchInvites_Players_InviteeId",
                        column: x => x.InviteeId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                    table.ForeignKey(
                        name: "FK_MatchInvites_Players_InviterId",
                        column: x => x.InviterId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                });

            migrationBuilder.CreateTable(
                name: "PlayerSettings",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    MessagePermission = table.Column<byte>(type: "tinyint", nullable: false),
                    ProfileVisibility = table.Column<byte>(type: "tinyint", nullable: false),
                    NotifyFollow = table.Column<bool>(type: "bit", nullable: false),
                    NotifyMatchInvite = table.Column<bool>(type: "bit", nullable: false),
                    NotifyTeamInvite = table.Column<bool>(type: "bit", nullable: false),
                    NotifyMatchReminder = table.Column<bool>(type: "bit", nullable: false),
                    NotifyMatchResult = table.Column<bool>(type: "bit", nullable: false),
                    NotifyMatchUpdates = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerSettings", x => x.PlayerId);
                    table.ForeignKey(
                        name: "FK_PlayerSettings_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    ReportId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReporterId = table.Column<int>(type: "int", nullable: false),
                    ReportedPlayerId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.ReportId);
                    table.ForeignKey(
                        name: "FK_Reports_Players_ReportedPlayerId",
                        column: x => x.ReportedPlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                    table.ForeignKey(
                        name: "FK_Reports_Players_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                });

            migrationBuilder.CreateTable(
                name: "TeamInvites",
                columns: table => new
                {
                    InviteId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    InviterId = table.Column<int>(type: "int", nullable: false),
                    InviteeId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamInvites", x => x.InviteId);
                    table.ForeignKey(
                        name: "FK_TeamInvites_Players_InviteeId",
                        column: x => x.InviteeId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                    table.ForeignKey(
                        name: "FK_TeamInvites_Players_InviterId",
                        column: x => x.InviterId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                    table.ForeignKey(
                        name: "FK_TeamInvites_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_ActorPlayerId_CreatedAt",
                table: "Activities",
                columns: new[] { "ActorPlayerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_MatchId_Type",
                table: "Activities",
                columns: new[] { "MatchId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_TargetPlayerId",
                table: "Activities",
                column: "TargetPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_TeamId",
                table: "Activities",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_BlockedId",
                table: "Blocks",
                column: "BlockedId");

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_BlockerId_BlockedId",
                table: "Blocks",
                columns: new[] { "BlockerId", "BlockedId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Follows_FolloweeId",
                table: "Follows",
                column: "FolloweeId");

            migrationBuilder.CreateIndex(
                name: "IX_Follows_FollowerId_FolloweeId",
                table: "Follows",
                columns: new[] { "FollowerId", "FolloweeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchInvites_InviteeId_Status",
                table: "MatchInvites",
                columns: new[] { "InviteeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchInvites_InviterId",
                table: "MatchInvites",
                column: "InviterId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchInvites_MatchId_InviteeId",
                table: "MatchInvites",
                columns: new[] { "MatchId", "InviteeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReportedPlayerId",
                table: "Reports",
                column: "ReportedPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReporterId",
                table: "Reports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_Status",
                table: "Reports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TeamInvites_InviteeId_Status",
                table: "TeamInvites",
                columns: new[] { "InviteeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamInvites_InviterId",
                table: "TeamInvites",
                column: "InviterId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamInvites_TeamId_InviteeId",
                table: "TeamInvites",
                columns: new[] { "TeamId", "InviteeId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MatchComments_Matchs_MatchId",
                table: "MatchComments",
                column: "MatchId",
                principalTable: "Matchs",
                principalColumn: "MatchId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchComments_Matchs_MatchId",
                table: "MatchComments");

            migrationBuilder.DropTable(
                name: "Activities");

            migrationBuilder.DropTable(
                name: "Blocks");

            migrationBuilder.DropTable(
                name: "Follows");

            migrationBuilder.DropTable(
                name: "MatchInvites");

            migrationBuilder.DropTable(
                name: "PlayerSettings");

            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropTable(
                name: "TeamInvites");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Players");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchComments_Matchs_MatchId",
                table: "MatchComments",
                column: "MatchId",
                principalTable: "Matchs",
                principalColumn: "MatchId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
