using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatcBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class TeamCreator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedByPlayerId",
                table: "Teams",
                type: "int",
                nullable: true);

            // Mevcut takımlar: kurucu, "takım kurdu" aktivitesinden alınır (hâlâ takımdaysa).
            // Bulunamayan eski takımlarda null kalır; onlarda eski kural (her üye yönetir) geçerlidir.
            migrationBuilder.Sql(@"
UPDATE t SET t.CreatedByPlayerId = a.ActorPlayerId
FROM Teams t
CROSS APPLY (
    SELECT TOP 1 ac.ActorPlayerId FROM Activities ac
    WHERE ac.TeamId = t.TeamId AND ac.Type = 5
    ORDER BY ac.ActivityId
) a
WHERE EXISTS (SELECT 1 FROM PlayerTeam pt WHERE pt.TeamId = t.TeamId AND pt.PlayerId = a.ActorPlayerId);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByPlayerId",
                table: "Teams");
        }
    }
}
