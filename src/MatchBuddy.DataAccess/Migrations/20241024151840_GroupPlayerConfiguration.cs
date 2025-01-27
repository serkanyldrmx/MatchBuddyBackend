using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatchBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class GroupPlayerConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupMessage_Group_GroupId",
                table: "GroupMessage");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupPlayer_GroupMessage_GroupMessageId",
                table: "GroupPlayer");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupPlayer_Players_PlayerId",
                table: "GroupPlayer");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GroupPlayer",
                table: "GroupPlayer");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GroupMessage",
                table: "GroupMessage");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Group",
                table: "Group");

            migrationBuilder.RenameTable(
                name: "GroupPlayer",
                newName: "GroupPlayers");

            migrationBuilder.RenameTable(
                name: "GroupMessage",
                newName: "GroupMessages");

            migrationBuilder.RenameTable(
                name: "Group",
                newName: "Groups");

            migrationBuilder.RenameColumn(
                name: "GroupMessageId",
                table: "GroupPlayers",
                newName: "GroupId");

            migrationBuilder.RenameIndex(
                name: "IX_GroupPlayer_PlayerId",
                table: "GroupPlayers",
                newName: "IX_GroupPlayers_PlayerId");

            migrationBuilder.RenameIndex(
                name: "IX_GroupPlayer_GroupMessageId",
                table: "GroupPlayers",
                newName: "IX_GroupPlayers_GroupId");

            migrationBuilder.RenameIndex(
                name: "IX_GroupMessage_GroupId",
                table: "GroupMessages",
                newName: "IX_GroupMessages_GroupId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GroupPlayers",
                table: "GroupPlayers",
                column: "GroupPlayerId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GroupMessages",
                table: "GroupMessages",
                column: "GroupMessageId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Groups",
                table: "Groups",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupMessages_Groups_GroupId",
                table: "GroupMessages",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "GroupId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupPlayers_Groups_GroupId",
                table: "GroupPlayers",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "GroupId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupPlayers_Players_PlayerId",
                table: "GroupPlayers",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupMessages_Groups_GroupId",
                table: "GroupMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupPlayers_Groups_GroupId",
                table: "GroupPlayers");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupPlayers_Players_PlayerId",
                table: "GroupPlayers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Groups",
                table: "Groups");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GroupPlayers",
                table: "GroupPlayers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GroupMessages",
                table: "GroupMessages");

            migrationBuilder.RenameTable(
                name: "Groups",
                newName: "Group");

            migrationBuilder.RenameTable(
                name: "GroupPlayers",
                newName: "GroupPlayer");

            migrationBuilder.RenameTable(
                name: "GroupMessages",
                newName: "GroupMessage");

            migrationBuilder.RenameColumn(
                name: "GroupId",
                table: "GroupPlayer",
                newName: "GroupMessageId");

            migrationBuilder.RenameIndex(
                name: "IX_GroupPlayers_PlayerId",
                table: "GroupPlayer",
                newName: "IX_GroupPlayer_PlayerId");

            migrationBuilder.RenameIndex(
                name: "IX_GroupPlayers_GroupId",
                table: "GroupPlayer",
                newName: "IX_GroupPlayer_GroupMessageId");

            migrationBuilder.RenameIndex(
                name: "IX_GroupMessages_GroupId",
                table: "GroupMessage",
                newName: "IX_GroupMessage_GroupId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Group",
                table: "Group",
                column: "GroupId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GroupPlayer",
                table: "GroupPlayer",
                column: "GroupPlayerId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GroupMessage",
                table: "GroupMessage",
                column: "GroupMessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupMessage_Group_GroupId",
                table: "GroupMessage",
                column: "GroupId",
                principalTable: "Group",
                principalColumn: "GroupId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupPlayer_GroupMessage_GroupMessageId",
                table: "GroupPlayer",
                column: "GroupMessageId",
                principalTable: "GroupMessage",
                principalColumn: "GroupMessageId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupPlayer_Players_PlayerId",
                table: "GroupPlayer",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
