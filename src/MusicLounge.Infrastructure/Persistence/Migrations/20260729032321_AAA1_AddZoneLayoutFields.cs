using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AAA1_AddZoneLayoutFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Layout2DHeight",
                table: "seating_zones",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Layout2DRotationDeg",
                table: "seating_zones",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Layout2DWidth",
                table: "seating_zones",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Layout2DX",
                table: "seating_zones",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Layout2DY",
                table: "seating_zones",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Layout3DX",
                table: "seating_zones",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Layout3DY",
                table: "seating_zones",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Layout3DZ",
                table: "seating_zones",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LayoutColor",
                table: "seating_zones",
                type: "nvarchar(9)",
                maxLength: 9,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Layout2DHeight",
                table: "seating_zones");

            migrationBuilder.DropColumn(
                name: "Layout2DRotationDeg",
                table: "seating_zones");

            migrationBuilder.DropColumn(
                name: "Layout2DWidth",
                table: "seating_zones");

            migrationBuilder.DropColumn(
                name: "Layout2DX",
                table: "seating_zones");

            migrationBuilder.DropColumn(
                name: "Layout2DY",
                table: "seating_zones");

            migrationBuilder.DropColumn(
                name: "Layout3DX",
                table: "seating_zones");

            migrationBuilder.DropColumn(
                name: "Layout3DY",
                table: "seating_zones");

            migrationBuilder.DropColumn(
                name: "Layout3DZ",
                table: "seating_zones");

            migrationBuilder.DropColumn(
                name: "LayoutColor",
                table: "seating_zones");
        }
    }
}
