using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLACP555_HotspotZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ZoneId",
                table: "venue_tour_hotspots",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_venue_tour_hotspots_ZoneId",
                table: "venue_tour_hotspots",
                column: "ZoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_venue_tour_hotspots_seating_zones_ZoneId",
                table: "venue_tour_hotspots",
                column: "ZoneId",
                principalTable: "seating_zones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_venue_tour_hotspots_seating_zones_ZoneId",
                table: "venue_tour_hotspots");

            migrationBuilder.DropIndex(
                name: "IX_venue_tour_hotspots_ZoneId",
                table: "venue_tour_hotspots");

            migrationBuilder.DropColumn(
                name: "ZoneId",
                table: "venue_tour_hotspots");
        }
    }
}
