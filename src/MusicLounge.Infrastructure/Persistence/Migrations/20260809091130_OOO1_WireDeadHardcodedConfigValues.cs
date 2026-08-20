using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OOO1_WireDeadHardcodedConfigValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "system_config",
                columns: new[] { "Id", "ConfigKey", "ConfigValue", "DataType", "Description", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 24, "publish_min_business_days_lead_time", "7", "Integer", "Min business days between publish/reschedule and show date — NĐ 144/2020 Điều 10", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 25, "penalty_suspension_notice_hours", "24", "Integer", "Notice hours before a Suspension penalty takes effect — §6.8", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 26, "penalty_ban_notice_days", "7", "Integer", "Notice days before a Ban penalty takes effect — §6.8", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 27, "ticket_hold_max_quantity", "10", "Integer", "Max tickets per checkout hold — anti-scalping ceiling", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 28, "walkin_ticket_max_quantity", "20", "Integer", "Max tickets per walk-in/box-office sale — anti-abuse ceiling", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 29, "donation_max_amount", "50000000", "Decimal", "Max single donation amount (VND) — anti-fraud ceiling", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 30, "ticket_transfer_expiry_hours", "48", "Integer", "Hours before an unanswered ticket-transfer request auto-cancels", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 30);
        }
    }
}
