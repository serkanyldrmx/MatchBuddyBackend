using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatchBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class EngagementFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LinkedStadiumId",
                table: "Stadiums",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Messages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Messages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReplyToMessageId",
                table: "Messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShareCode",
                table: "Matchs",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "GroupMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "GroupMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReplyToGroupMessageId",
                table: "GroupMessages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MessageReactions",
                columns: table => new
                {
                    ReactionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<int>(type: "int", nullable: true),
                    GroupMessageId = table.Column<int>(type: "int", nullable: true),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    Emoji = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageReactions", x => x.ReactionId);
                    table.ForeignKey(
                        name: "FK_MessageReactions_GroupMessages_GroupMessageId",
                        column: x => x.GroupMessageId,
                        principalTable: "GroupMessages",
                        principalColumn: "GroupMessageId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageReactions_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageReactions_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Stadiums_LinkedStadiumId",
                table: "Stadiums",
                column: "LinkedStadiumId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReplyToMessageId",
                table: "Messages",
                column: "ReplyToMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Matchs_ShareCode",
                table: "Matchs",
                column: "ShareCode",
                unique: true,
                filter: "[ShareCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessages_ReplyToGroupMessageId",
                table: "GroupMessages",
                column: "ReplyToGroupMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReactions_GroupMessageId_PlayerId",
                table: "MessageReactions",
                columns: new[] { "GroupMessageId", "PlayerId" },
                unique: true,
                filter: "[GroupMessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReactions_MessageId_PlayerId",
                table: "MessageReactions",
                columns: new[] { "MessageId", "PlayerId" },
                unique: true,
                filter: "[MessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReactions_PlayerId",
                table: "MessageReactions",
                column: "PlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupMessages_GroupMessages_ReplyToGroupMessageId",
                table: "GroupMessages",
                column: "ReplyToGroupMessageId",
                principalTable: "GroupMessages",
                principalColumn: "GroupMessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Messages_ReplyToMessageId",
                table: "Messages",
                column: "ReplyToMessageId",
                principalTable: "Messages",
                principalColumn: "MessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_Stadiums_Stadiums_LinkedStadiumId",
                table: "Stadiums",
                column: "LinkedStadiumId",
                principalTable: "Stadiums",
                principalColumn: "StadiumId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupMessages_GroupMessages_ReplyToGroupMessageId",
                table: "GroupMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Messages_ReplyToMessageId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Stadiums_Stadiums_LinkedStadiumId",
                table: "Stadiums");

            migrationBuilder.DropTable(
                name: "MessageReactions");

            migrationBuilder.DropIndex(
                name: "IX_Stadiums_LinkedStadiumId",
                table: "Stadiums");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ReplyToMessageId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Matchs_ShareCode",
                table: "Matchs");

            migrationBuilder.DropIndex(
                name: "IX_GroupMessages_ReplyToGroupMessageId",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "LinkedStadiumId",
                table: "Stadiums");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ReplyToMessageId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ShareCode",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "ReplyToGroupMessageId",
                table: "GroupMessages");
        }
    }
}
