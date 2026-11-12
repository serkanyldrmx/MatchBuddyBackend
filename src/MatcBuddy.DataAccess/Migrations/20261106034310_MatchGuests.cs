using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatcBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class MatchGuests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchGuests",
                columns: table => new
                {
                    GuestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Side = table.Column<byte>(type: "tinyint", nullable: false),
                    AddedByPlayerId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchGuests", x => x.GuestId);
                    table.ForeignKey(
                        name: "FK_MatchGuests_Matchs_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matchs",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchGuests_Players_AddedByPlayerId",
                        column: x => x.AddedByPlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchGuests_AddedByPlayerId",
                table: "MatchGuests",
                column: "AddedByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchGuests_MatchId",
                table: "MatchGuests",
                column: "MatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchGuests");
        }
    }
}
