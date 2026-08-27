using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniSteam.Migrations
{
    /// <inheritdoc />
    public partial class ProtectPurchaseHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseItems_Games_GameId",
                table: "PurchaseItems");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseItems_Games_GameId",
                table: "PurchaseItems",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseItems_Games_GameId",
                table: "PurchaseItems");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseItems_Games_GameId",
                table: "PurchaseItems",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
