using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LL2_FixTicketPriceSpuriousFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketPrices_seating_zones_SeatingZoneId",
                table: "TicketPrices");

            migrationBuilder.DropIndex(
                name: "IX_TicketPrices_SeatingZoneId",
                table: "TicketPrices");

            migrationBuilder.DropColumn(
                name: "SeatingZoneId",
                table: "TicketPrices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeatingZoneId",
                table: "TicketPrices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketPrices_SeatingZoneId",
                table: "TicketPrices",
                column: "SeatingZoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketPrices_seating_zones_SeatingZoneId",
                table: "TicketPrices",
                column: "SeatingZoneId",
                principalTable: "seating_zones",
                principalColumn: "Id");
        }
    }
}
