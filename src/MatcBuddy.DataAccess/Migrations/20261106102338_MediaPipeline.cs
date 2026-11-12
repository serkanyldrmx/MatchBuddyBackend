using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatcBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class MediaPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MediaId",
                table: "StadiumPhotos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProfileMediaId",
                table: "Players",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Messages",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediaId",
                table: "Messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "GroupMessages",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediaId",
                table: "GroupMessages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Media",
                columns: table => new
                {
                    MediaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    KeyPrefix = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OwnerPlayerId = table.Column<int>(type: "int", nullable: true),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    Bytes = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttachedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Media", x => x.MediaId);
                    table.ForeignKey(
                        name: "FK_Media_Players_OwnerPlayerId",
                        column: x => x.OwnerPlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Media_KeyPrefix",
                table: "Media",
                column: "KeyPrefix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Media_OwnerPlayerId",
                table: "Media",
                column: "OwnerPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Media_Status_CreatedAt",
                table: "Media",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Media");

            migrationBuilder.DropColumn(
                name: "MediaId",
                table: "StadiumPhotos");

            migrationBuilder.DropColumn(
                name: "ProfileMediaId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "MediaId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "MediaId",
                table: "GroupMessages");
        }
    }
}
