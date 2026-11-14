using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatcBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class MatchLineupSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Slot",
                table: "MatchParticipants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Slot",
                table: "MatchGuests",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Slot",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "Slot",
                table: "MatchGuests");
        }
    }
}
