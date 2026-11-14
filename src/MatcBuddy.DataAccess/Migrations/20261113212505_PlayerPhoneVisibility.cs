using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatcBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class PlayerPhoneVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "PhoneVisibility",
                table: "PlayerSettings",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1); // mevcut oyuncular: "maç yöneticileri" (PhoneVisibilities.MatchManagers)
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhoneVisibility",
                table: "PlayerSettings");
        }
    }
}
