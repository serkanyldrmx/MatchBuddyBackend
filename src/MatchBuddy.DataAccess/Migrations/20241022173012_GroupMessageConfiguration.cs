using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatchBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class GroupMessageConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GroupMessage",
                columns: table => new
                {
                    GroupMessageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    MatchMessage = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    SendingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SendPlayerId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupMessage", x => x.GroupMessageId);
                });

            migrationBuilder.CreateTable(
                name: "GroupPlayer",
                columns: table => new
                {
                    GroupPlayerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupMessageId = table.Column<int>(type: "int", nullable: false),
                    RecipientPlayerId = table.Column<int>(type: "int", nullable: true),
                    PlayerId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupPlayer", x => x.GroupPlayerId);
                    table.ForeignKey(
                        name: "FK_GroupPlayer_GroupMessage_GroupMessageId",
                        column: x => x.GroupMessageId,
                        principalTable: "GroupMessage",
                        principalColumn: "GroupMessageId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupPlayer_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupPlayer_GroupMessageId",
                table: "GroupPlayer",
                column: "GroupMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPlayer_PlayerId",
                table: "GroupPlayer",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupPlayer");

            migrationBuilder.DropTable(
                name: "GroupMessage");
        }
    }
}
