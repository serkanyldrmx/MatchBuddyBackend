using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatcBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class LaunchImprovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "ResolutionAction",
                table: "Reports",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionNote",
                table: "Reports",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "Reports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResolvedByPlayerId",
                table: "Reports",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BanReason",
                table: "Players",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BannedUntil",
                table: "Players",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBanned",
                table: "Players",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Matchs",
                type: "datetime",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPaid",
                table: "Matchs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LateCancelledAt",
                table: "Matchs",
                type: "datetime",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NoShow",
                table: "Matchs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte>(
                name: "Visibility",
                table: "Matchs",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.CreateTable(
                name: "ContactMessages",
                columns: table => new
                {
                    ContactMessageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: true),
                    IsHandled = table.Column<bool>(type: "bit", nullable: false),
                    HandledAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactMessages", x => x.ContactMessageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContactMessages_IsHandled_CreatedAt",
                table: "ContactMessages",
                columns: new[] { "IsHandled", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactMessages_PlayerId",
                table: "ContactMessages",
                column: "PlayerId");

            // Ödendi işareti gelmeden önce onaylanmış ve oynanmış tesis maçları tahsil edilmiş sayılır (geçmiş ciro korunur)
            migrationBuilder.Sql("EXEC(N'UPDATE [Matchs] SET [IsPaid] = 1 WHERE [IsActive] = 2 AND [PitchId] IS NOT NULL AND [MatchDate] < GETDATE()')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContactMessages");

            migrationBuilder.DropColumn(
                name: "ResolutionAction",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ResolutionNote",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ResolvedByPlayerId",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "BanReason",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "BannedUntil",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "IsBanned",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "IsPaid",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "LateCancelledAt",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "NoShow",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "Matchs");
        }
    }
}
