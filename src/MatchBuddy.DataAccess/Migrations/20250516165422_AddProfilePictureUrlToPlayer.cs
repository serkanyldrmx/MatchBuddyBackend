using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatchBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddProfilePictureUrlToPlayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfilePictureUrl",
                table: "Players",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfilePictureUrl",
                table: "Players");
        }
    }
}
