using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatcBuddy.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class WebPush : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PushedAt",
                table: "PlayerNotification",
                type: "datetime2",
                nullable: true);

            // Mevcut bildirimler gönderilmiş sayılır (telefonlara geçmiş bildirim yağmasın)
            migrationBuilder.Sql("UPDATE [PlayerNotification] SET [PushedAt] = [SentAt] WHERE [PushedAt] IS NULL");

            migrationBuilder.CreateTable(
                name: "PushSubscriptions",
                columns: table => new
                {
                    PushSubscriptionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    Endpoint = table.Column<string>(type: "varchar(800)", unicode: false, maxLength: 800, nullable: false),
                    P256dh = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    Auth = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushSubscriptions", x => x.PushSubscriptionId);
                    table.ForeignKey(
                        name: "FK_PushSubscriptions_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerNotification_PushedAt",
                table: "PlayerNotification",
                column: "PushedAt",
                filter: "[PushedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_Endpoint",
                table: "PushSubscriptions",
                column: "Endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_PlayerId",
                table: "PushSubscriptions",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PushSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_PlayerNotification_PushedAt",
                table: "PlayerNotification");

            migrationBuilder.DropColumn(
                name: "PushedAt",
                table: "PlayerNotification");
        }
    }
}
