using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatchBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class PlatformParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "PlayerSettings",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "tr");

            migrationBuilder.AddColumn<string>(
                name: "Theme",
                table: "PlayerSettings",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "system");

            migrationBuilder.CreateTable(
                name: "SystemParameters",
                columns: table => new
                {
                    SystemParameterId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DataType = table.Column<byte>(type: "tinyint", nullable: false),
                    Group = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MinValue = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MaxValue = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByPlayerId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemParameters", x => x.SystemParameterId);
                    table.ForeignKey(
                        name: "FK_SystemParameters_Players_UpdatedByPlayerId",
                        column: x => x.UpdatedByPlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                });

            migrationBuilder.CreateTable(
                name: "VenueSettings",
                columns: table => new
                {
                    StadiumId = table.Column<int>(type: "int", nullable: false),
                    MinBookingNoticeHours = table.Column<int>(type: "int", nullable: true),
                    FreeCancellationHours = table.Column<int>(type: "int", nullable: true),
                    MaxAdvanceBookingDays = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByPlayerId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VenueSettings", x => x.StadiumId);
                    table.ForeignKey(
                        name: "FK_VenueSettings_Players_UpdatedByPlayerId",
                        column: x => x.UpdatedByPlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                    table.ForeignKey(
                        name: "FK_VenueSettings_Stadiums_StadiumId",
                        column: x => x.StadiumId,
                        principalTable: "Stadiums",
                        principalColumn: "StadiumId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParameterAudits",
                columns: table => new
                {
                    ParameterAuditId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SystemParameterId = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ChangedByPlayerId = table.Column<int>(type: "int", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterAudits", x => x.ParameterAuditId);
                    table.ForeignKey(
                        name: "FK_ParameterAudits_Players_ChangedByPlayerId",
                        column: x => x.ChangedByPlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                    table.ForeignKey(
                        name: "FK_ParameterAudits_SystemParameters_SystemParameterId",
                        column: x => x.SystemParameterId,
                        principalTable: "SystemParameters",
                        principalColumn: "SystemParameterId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParameterAudits_ChangedByPlayerId",
                table: "ParameterAudits",
                column: "ChangedByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_ParameterAudits_SystemParameterId_ChangedAt",
                table: "ParameterAudits",
                columns: new[] { "SystemParameterId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemParameters_Key",
                table: "SystemParameters",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemParameters_UpdatedByPlayerId",
                table: "SystemParameters",
                column: "UpdatedByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_VenueSettings_UpdatedByPlayerId",
                table: "VenueSettings",
                column: "UpdatedByPlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParameterAudits");

            migrationBuilder.DropTable(
                name: "VenueSettings");

            migrationBuilder.DropTable(
                name: "SystemParameters");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "PlayerSettings");

            migrationBuilder.DropColumn(
                name: "Theme",
                table: "PlayerSettings");
        }
    }
}
