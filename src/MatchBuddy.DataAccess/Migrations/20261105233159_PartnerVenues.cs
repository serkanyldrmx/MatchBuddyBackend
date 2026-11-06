using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatchBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class PartnerVenues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "District",
                table: "Stadiums",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Stadiums",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CameraPrice",
                table: "Stadiums",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationPolicy",
                table: "Stadiums",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CleatRental",
                table: "Stadiums",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "CleatRentalPrice",
                table: "Stadiums",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Stadiums",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()");

            migrationBuilder.AddColumn<bool>(
                name: "HasCafe",
                table: "Stadiums",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasCamera",
                table: "Stadiums",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasLockerRoom",
                table: "Stadiums",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasParking",
                table: "Stadiums",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasShower",
                table: "Stadiums",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasShuttle",
                table: "Stadiums",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Admin "yayında" anahtarı: varsayılan 1 (kodda da varsayılan true)
            migrationBuilder.AddColumn<bool>(
                name: "IsListed",
                table: "Stadiums",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<long>(
                name: "OsmId",
                table: "Stadiums",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OwnerPlayerId",
                table: "Stadiums",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Stadiums",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ProvidesBall",
                table: "Stadiums",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ProvidesBibs",
                table: "Stadiums",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MembershipSuspended",
                table: "Players",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MembershipUntil",
                table: "Players",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Role",
                table: "Players",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<int>(
                name: "AwayTeamId",
                table: "Matchs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CameraRequested",
                table: "Matchs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CleatCount",
                table: "Matchs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Matchs",
                type: "datetime",
                nullable: false,
                defaultValueSql: "GETDATE()");

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "Matchs",
                type: "int",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<decimal>(
                name: "ExtrasPrice",
                table: "Matchs",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeTeamId",
                table: "Matchs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LookingForOpponent",
                table: "Matchs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PitchId",
                table: "Matchs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PitchPrice",
                table: "Matchs",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "Matchs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPrice",
                table: "Matchs",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Side",
                table: "MatchInvites",
                type: "tinyint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OpeningHours",
                columns: table => new
                {
                    OpeningHourId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StadiumId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<byte>(type: "tinyint", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    OpenTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    CloseTime = table.Column<TimeSpan>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningHours", x => x.OpeningHourId);
                    table.ForeignKey(
                        name: "FK_OpeningHours_Stadiums_StadiumId",
                        column: x => x.StadiumId,
                        principalTable: "Stadiums",
                        principalColumn: "StadiumId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pitches",
                columns: table => new
                {
                    PitchId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StadiumId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Surface = table.Column<byte>(type: "tinyint", nullable: false),
                    IsIndoor = table.Column<bool>(type: "bit", nullable: false),
                    PlayersPerSide = table.Column<int>(type: "int", nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pitches", x => x.PitchId);
                    table.ForeignKey(
                        name: "FK_Pitches_Stadiums_StadiumId",
                        column: x => x.StadiumId,
                        principalTable: "Stadiums",
                        principalColumn: "StadiumId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StadiumPhotos",
                columns: table => new
                {
                    PhotoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StadiumId = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsCover = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StadiumPhotos", x => x.PhotoId);
                    table.ForeignKey(
                        name: "FK_StadiumPhotos_Stadiums_StadiumId",
                        column: x => x.StadiumId,
                        principalTable: "Stadiums",
                        principalColumn: "StadiumId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VenueReviews",
                columns: table => new
                {
                    ReviewId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StadiumId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    Rating = table.Column<byte>(type: "tinyint", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OwnerReply = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RepliedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VenueReviews", x => x.ReviewId);
                    table.ForeignKey(
                        name: "FK_VenueReviews_Matchs_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matchs",
                        principalColumn: "MatchId");
                    table.ForeignKey(
                        name: "FK_VenueReviews_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId");
                    table.ForeignKey(
                        name: "FK_VenueReviews_Stadiums_StadiumId",
                        column: x => x.StadiumId,
                        principalTable: "Stadiums",
                        principalColumn: "StadiumId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    CampaignId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StadiumId = table.Column<int>(type: "int", nullable: false),
                    PitchId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    DaysMask = table.Column<int>(type: "int", nullable: false),
                    FromHour = table.Column<int>(type: "int", nullable: false),
                    ToHour = table.Column<int>(type: "int", nullable: false),
                    DiscountPercent = table.Column<int>(type: "int", nullable: true),
                    FixedPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.CampaignId);
                    table.ForeignKey(
                        name: "FK_Campaigns_Pitches_PitchId",
                        column: x => x.PitchId,
                        principalTable: "Pitches",
                        principalColumn: "PitchId");
                    table.ForeignKey(
                        name: "FK_Campaigns_Stadiums_StadiumId",
                        column: x => x.StadiumId,
                        principalTable: "Stadiums",
                        principalColumn: "StadiumId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PriceRules",
                columns: table => new
                {
                    PriceRuleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StadiumId = table.Column<int>(type: "int", nullable: false),
                    PitchId = table.Column<int>(type: "int", nullable: true),
                    DaysMask = table.Column<int>(type: "int", nullable: false),
                    FromHour = table.Column<int>(type: "int", nullable: false),
                    ToHour = table.Column<int>(type: "int", nullable: false),
                    PricePerHour = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceRules", x => x.PriceRuleId);
                    table.ForeignKey(
                        name: "FK_PriceRules_Pitches_PitchId",
                        column: x => x.PitchId,
                        principalTable: "Pitches",
                        principalColumn: "PitchId");
                    table.ForeignKey(
                        name: "FK_PriceRules_Stadiums_StadiumId",
                        column: x => x.StadiumId,
                        principalTable: "Stadiums",
                        principalColumn: "StadiumId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SlotBlocks",
                columns: table => new
                {
                    SlotBlockId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PitchId = table.Column<int>(type: "int", nullable: false),
                    StartAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlotBlocks", x => x.SlotBlockId);
                    table.ForeignKey(
                        name: "FK_SlotBlocks_Pitches_PitchId",
                        column: x => x.PitchId,
                        principalTable: "Pitches",
                        principalColumn: "PitchId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Stadiums_OsmId",
                table: "Stadiums",
                column: "OsmId");

            migrationBuilder.CreateIndex(
                name: "IX_Stadiums_OwnerPlayerId",
                table: "Stadiums",
                column: "OwnerPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Matchs_AwayTeamId",
                table: "Matchs",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Matchs_HomeTeamId",
                table: "Matchs",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Matchs_PitchId_MatchDate",
                table: "Matchs",
                columns: new[] { "PitchId", "MatchDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_PitchId",
                table: "Campaigns",
                column: "PitchId");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_StadiumId",
                table: "Campaigns",
                column: "StadiumId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningHours_StadiumId_DayOfWeek",
                table: "OpeningHours",
                columns: new[] { "StadiumId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pitches_StadiumId",
                table: "Pitches",
                column: "StadiumId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceRules_PitchId",
                table: "PriceRules",
                column: "PitchId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceRules_StadiumId",
                table: "PriceRules",
                column: "StadiumId");

            migrationBuilder.CreateIndex(
                name: "IX_SlotBlocks_PitchId_StartAt",
                table: "SlotBlocks",
                columns: new[] { "PitchId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StadiumPhotos_StadiumId_SortOrder",
                table: "StadiumPhotos",
                columns: new[] { "StadiumId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_VenueReviews_MatchId_PlayerId",
                table: "VenueReviews",
                columns: new[] { "MatchId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VenueReviews_PlayerId",
                table: "VenueReviews",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_VenueReviews_StadiumId_CreatedAt",
                table: "VenueReviews",
                columns: new[] { "StadiumId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Matchs_Pitches_PitchId",
                table: "Matchs",
                column: "PitchId",
                principalTable: "Pitches",
                principalColumn: "PitchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Matchs_Teams_AwayTeamId",
                table: "Matchs",
                column: "AwayTeamId",
                principalTable: "Teams",
                principalColumn: "TeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_Matchs_Teams_HomeTeamId",
                table: "Matchs",
                column: "HomeTeamId",
                principalTable: "Teams",
                principalColumn: "TeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_Stadiums_Players_OwnerPlayerId",
                table: "Stadiums",
                column: "OwnerPlayerId",
                principalTable: "Players",
                principalColumn: "PlayerId");

            // Rol: IsAdmin geriye uyumluluk için kalır, Role ile senkron
            migrationBuilder.Sql("UPDATE Players SET Role = 1 WHERE IsAdmin = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matchs_Pitches_PitchId",
                table: "Matchs");

            migrationBuilder.DropForeignKey(
                name: "FK_Matchs_Teams_AwayTeamId",
                table: "Matchs");

            migrationBuilder.DropForeignKey(
                name: "FK_Matchs_Teams_HomeTeamId",
                table: "Matchs");

            migrationBuilder.DropForeignKey(
                name: "FK_Stadiums_Players_OwnerPlayerId",
                table: "Stadiums");

            migrationBuilder.DropTable(
                name: "Campaigns");

            migrationBuilder.DropTable(
                name: "OpeningHours");

            migrationBuilder.DropTable(
                name: "PriceRules");

            migrationBuilder.DropTable(
                name: "SlotBlocks");

            migrationBuilder.DropTable(
                name: "StadiumPhotos");

            migrationBuilder.DropTable(
                name: "VenueReviews");

            migrationBuilder.DropTable(
                name: "Pitches");

            migrationBuilder.DropIndex(
                name: "IX_Stadiums_OsmId",
                table: "Stadiums");

            migrationBuilder.DropIndex(
                name: "IX_Stadiums_OwnerPlayerId",
                table: "Stadiums");

            migrationBuilder.DropIndex(
                name: "IX_Matchs_AwayTeamId",
                table: "Matchs");

            migrationBuilder.DropIndex(
                name: "IX_Matchs_HomeTeamId",
                table: "Matchs");

            migrationBuilder.DropIndex(
                name: "IX_Matchs_PitchId_MatchDate",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "CameraPrice",
                table: "Stadiums");

            migrationBuilder.DropColumn(
                name: "CancellationPolicy",
                table: "Stadiums");

            migrationBuilder.DropColumn(
                name: "CleatRental",
                table: "Stadiums");

            migrationBuilder.DropColumn(
                name: "CleatRentalPrice",
                table: "Stadiums");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Stadiums");

            migrationBuilder.DropColumn(
                name: "HasCafe",
                table: "Stadiums");

            migrationBuilder.DropColumn(
                name: "HasCamera",
                table: "Stadiums");

            migrationBuilder.DropColumn(
                name: "HasLockerRoom",
                table: "Stadiums");

            migrationBuilder.DropColumn(
                name: "HasParking",
                table: "Stadiums");

            migrationBuilder.DropColumn(
                name: "HasShower",
                table: "Stadiums");

            migrationBuilder.DropColumn(
                name: "HasShuttle",
                table: "Stadiums");

            migrationBuilder.DropColumn(
                name: "IsListed",
                table: "Stadiums");

            migrationBuilder.DropColumn(
                name: "OsmId",
                table: "Stadiums");

            migrationBuilder.DropColumn(
                name: "OwnerPlayerId",
                table: "Stadiums");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Stadiums");

            migrationBuilder.DropColumn(
                name: "ProvidesBall",
                table: "Stadiums");

            migrationBuilder.DropColumn(
                name: "ProvidesBibs",
                table: "Stadiums");

            migrationBuilder.DropColumn(
                name: "MembershipSuspended",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "MembershipUntil",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "AwayTeamId",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "CameraRequested",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "CleatCount",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "ExtrasPrice",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "HomeTeamId",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "LookingForOpponent",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "PitchId",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "PitchPrice",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "TotalPrice",
                table: "Matchs");

            migrationBuilder.DropColumn(
                name: "Side",
                table: "MatchInvites");

            migrationBuilder.AlterColumn<int>(
                name: "District",
                table: "Stadiums",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(60)",
                oldMaxLength: 60,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "City",
                table: "Stadiums",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(60)",
                oldMaxLength: 60,
                oldNullable: true);
        }
    }
}
