using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatcBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class EmailCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailCampaigns",
                columns: table => new
                {
                    EmailCampaignId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedByPlayerId = table.Column<int>(type: "int", nullable: false),
                    OwnerPlayerId = table.Column<int>(type: "int", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    ButtonText = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ButtonUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Audience = table.Column<byte>(type: "tinyint", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RepeatDays = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Cursor = table.Column<int>(type: "int", nullable: false),
                    SentCount = table.Column<int>(type: "int", nullable: false),
                    LastSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailCampaigns", x => x.EmailCampaignId);
                });

            migrationBuilder.CreateTable(
                name: "EmailTemplates",
                columns: table => new
                {
                    EmailTemplateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    ButtonText = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ButtonUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    OwnerPlayerId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplates", x => x.EmailTemplateId);
                });

            migrationBuilder.CreateTable(
                name: "MatchEmailReminders",
                columns: table => new
                {
                    MatchEmailReminderId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    MinutesBefore = table.Column<int>(type: "int", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchEmailReminders", x => x.MatchEmailReminderId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaigns_OwnerPlayerId",
                table: "EmailCampaigns",
                column: "OwnerPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaigns_Status_ScheduledAt",
                table: "EmailCampaigns",
                columns: new[] { "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_OwnerPlayerId_Key",
                table: "EmailTemplates",
                columns: new[] { "OwnerPlayerId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchEmailReminders_MatchId_PlayerId_MinutesBefore",
                table: "MatchEmailReminders",
                columns: new[] { "MatchId", "PlayerId", "MinutesBefore" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchEmailReminders_SentAt",
                table: "MatchEmailReminders",
                column: "SentAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailCampaigns");

            migrationBuilder.DropTable(
                name: "EmailTemplates");

            migrationBuilder.DropTable(
                name: "MatchEmailReminders");
        }
    }
}
