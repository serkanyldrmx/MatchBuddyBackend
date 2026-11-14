using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatcBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ExternalVenuesAndChatReadState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedByPlayerId",
                table: "Stadiums",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExternal",
                table: "Stadiums",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "MatchMessage",
                table: "Messages",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAt",
                table: "Messages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByPlayerId",
                table: "Groups",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReadAt",
                table: "GroupPlayers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MatchMessage",
                table: "GroupMessages",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250);

            // Okundu bilgisi bu sürümle başlıyor: mevcut mesajlar okunmuş sayılır (herkes eski mesajlar için rozet görmesin)
            migrationBuilder.Sql("UPDATE Messages SET ReadAt = SendingDate WHERE ReadAt IS NULL");
            migrationBuilder.Sql("UPDATE GroupPlayers SET LastReadAt = GETDATE() WHERE LastReadAt IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Stadiums_CreatedByPlayerId_IsExternal",
                table: "Stadiums",
                columns: new[] { "CreatedByPlayerId", "IsExternal" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_RecipientPlayerId_SendPlayerId",
                table: "Messages",
                columns: new[] { "RecipientPlayerId", "SendPlayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Groups_CreatedByPlayerId",
                table: "Groups",
                column: "CreatedByPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Groups_Players_CreatedByPlayerId",
                table: "Groups",
                column: "CreatedByPlayerId",
                principalTable: "Players",
                principalColumn: "PlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Stadiums_Players_CreatedByPlayerId",
                table: "Stadiums",
                column: "CreatedByPlayerId",
                principalTable: "Players",
                principalColumn: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Groups_Players_CreatedByPlayerId",
                table: "Groups");

            migrationBuilder.DropForeignKey(
                name: "FK_Stadiums_Players_CreatedByPlayerId",
                table: "Stadiums");

            migrationBuilder.DropIndex(
                name: "IX_Stadiums_CreatedByPlayerId_IsExternal",
                table: "Stadiums");

            migrationBuilder.DropIndex(
                name: "IX_Messages_RecipientPlayerId_SendPlayerId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Groups_CreatedByPlayerId",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "CreatedByPlayerId",
                table: "Stadiums");

            migrationBuilder.DropColumn(
                name: "IsExternal",
                table: "Stadiums");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "CreatedByPlayerId",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "LastReadAt",
                table: "GroupPlayers");

            migrationBuilder.AlterColumn<string>(
                name: "MatchMessage",
                table: "Messages",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "MatchMessage",
                table: "GroupMessages",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);
        }
    }
}
