using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatcBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class BusinessApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessApplications",
                columns: table => new
                {
                    BusinessApplicationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    City = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    District = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    PitchCount = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    AdminNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessApplications", x => x.BusinessApplicationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessApplications_Status_CreatedAt",
                table: "BusinessApplications",
                columns: new[] { "Status", "CreatedAt" });

            // Takımlarda iki kez kayıtlı oyuncular (eski kayıt akışı aynı oyuncuyu tekrar ekleyebiliyordu): ilk kayıt kalır
            migrationBuilder.Sql(@"
DELETE pt FROM PlayerTeam pt
WHERE EXISTS (SELECT 1 FROM PlayerTeam o WHERE o.TeamId = pt.TeamId AND o.PlayerId = pt.PlayerId AND o.PlayerTeamId < pt.PlayerTeamId);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessApplications");
        }
    }
}
