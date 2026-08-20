using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DonationShareRateSnapshotAndDropAutoRenew : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoRenew",
                table: "owner_subscriptions");

            migrationBuilder.AddColumn<decimal>(
                name: "PerformerShareRateSnapshot",
                table: "donations",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PerformerShareRateSnapshot",
                table: "donations");

            migrationBuilder.AddColumn<bool>(
                name: "AutoRenew",
                table: "owner_subscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
