using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLACP375_SubscriptionCompensationTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompensatedSubscriptionId",
                table: "venue_penalties",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SubscriptionCompensationDays",
                table: "venue_penalties",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubscriptionCompensationFrom",
                table: "venue_penalties",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompensatedSubscriptionId",
                table: "venue_penalties");

            migrationBuilder.DropColumn(
                name: "SubscriptionCompensationDays",
                table: "venue_penalties");

            migrationBuilder.DropColumn(
                name: "SubscriptionCompensationFrom",
                table: "venue_penalties");
        }
    }
}
