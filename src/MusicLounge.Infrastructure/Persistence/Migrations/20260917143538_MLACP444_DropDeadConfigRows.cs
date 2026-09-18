using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLACP444_DropDeadConfigRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 14);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "system_config",
                columns: new[] { "Id", "ConfigKey", "ConfigValue", "DataType", "Description", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "gateway_fee_rate", "0.02", "Decimal", "VNPay gateway processing fee (2%) — mức thương mại của cổng, không do văn bản pháp luật ấn định", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 13, "ai_priority_high_threshold", "0.60", "Decimal", "AI score ≥ this → urgent queue for Admin review — D11", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 14, "ai_priority_low_threshold", "0.20", "Decimal", "AI score ≤ this → normal queue for Admin review — D11", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null }
                });
        }
    }
}
