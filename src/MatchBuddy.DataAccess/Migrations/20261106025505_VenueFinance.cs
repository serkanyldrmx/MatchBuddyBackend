using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatchBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class VenueFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "SlotBlocks",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerPhone",
                table: "SlotBlocks",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPaid",
                table: "SlotBlocks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte>(
                name: "Kind",
                table: "SlotBlocks",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "SlotBlocks",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "SlotBlocks",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VenueExpenses",
                columns: table => new
                {
                    ExpenseId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StadiumId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VenueExpenses", x => x.ExpenseId);
                    table.ForeignKey(
                        name: "FK_VenueExpenses_Stadiums_StadiumId",
                        column: x => x.StadiumId,
                        principalTable: "Stadiums",
                        principalColumn: "StadiumId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VenueExpenses_StadiumId_Date",
                table: "VenueExpenses",
                columns: new[] { "StadiumId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VenueExpenses");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "SlotBlocks");

            migrationBuilder.DropColumn(
                name: "CustomerPhone",
                table: "SlotBlocks");

            migrationBuilder.DropColumn(
                name: "IsPaid",
                table: "SlotBlocks");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "SlotBlocks");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "SlotBlocks");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "SlotBlocks");
        }
    }
}
