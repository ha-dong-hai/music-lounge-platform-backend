using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OO1_RequirementsGapFix : Migration
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
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "TicketPrices",
                type: "decimal(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "TicketPrices",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "TicketPrices",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Sold",
                table: "TicketPrices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Performances",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Main");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "SetTime",
                table: "Performances",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Method",
                table: "payments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Gateway");

            migrationBuilder.AddColumn<int>(
                name: "PayerId",
                table: "payments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementStatus",
                table: "payments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NotApplicable");

            migrationBuilder.AlterColumn<decimal>(
                name: "ReputationScore",
                table: "Lounges",
                type: "decimal(3,2)",
                precision: 3,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 2,
                oldDefaultValue: 100m);

            migrationBuilder.AddColumn<int>(
                name: "AtmosphereId",
                table: "Lounges",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Lounges",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LivestreamId",
                table: "livestream_ticket_details",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 4,
                column: "Description",
                value: "Days before scheduled_start to release partial settlement");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 5,
                column: "Description",
                value: "Days after actual_end to release final settlement");

            migrationBuilder.InsertData(
                table: "system_config",
                columns: new[] { "Id", "ConfigKey", "ConfigValue", "DataType", "Description", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "gateway_fee_rate", "0.02", "Decimal", "VNPay gateway processing fee (2%) — NĐ 52/2024", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 2, "platform_commission_rate", "0.05", "Decimal", "Platform fee rate (5%) — NĐ 117/2025", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 3, "tax_rate", "0.05", "Decimal", "VAT withheld at source (5%) — NĐ 117/2025", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 6, "settlement_completion_threshold_pct", "0.70", "Decimal", "D16: min actual/scheduled ratio for auto-release final settlement", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 7, "settlement_tier_new_pre_rate", "0.50", "Decimal", "D3 Tier Mới: pre_rate for venues score<3.5 or <3 shows", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 8, "settlement_tier_standard_pre_rate", "0.70", "Decimal", "D3 Tier Chuẩn: pre_rate for venues score 3.5–4.2", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 9, "settlement_tier_premium_pre_rate", "0.80", "Decimal", "D3 Tier Premium: pre_rate for venues score≥4.2 AND ≥10 shows", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 10, "settlement_tier_standard_min_score", "3.5", "Decimal", "D3: reputation_score threshold to qualify for Tier Chuẩn", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 11, "settlement_tier_premium_min_score", "4.2", "Decimal", "D3: reputation_score threshold to qualify for Tier Premium", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 12, "settlement_tier_premium_min_shows", "10", "Integer", "D3: minimum completed shows to qualify for Tier Premium", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 13, "ai_priority_high_threshold", "0.60", "Decimal", "AI score ≥ this → urgent queue for Admin review — D11", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 14, "ai_priority_low_threshold", "0.20", "Decimal", "AI score ≤ this → normal queue for Admin review — D11", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 15, "moderation_sla_hours", "24", "Integer", "Admin SLA to review flagged content — NĐ 147/2024", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 16, "ticket_hold_minutes", "15", "Integer", "Checkout hold duration before slot released — §6.3", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 17, "donation_hold_days", "7", "Integer", "Days before auto-confirm donation if Owner inactive — D4", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 18, "rating_window_days", "7", "Integer", "Days after show end to submit rating — §6.13", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 19, "appeal_sla_hours", "48", "Integer", "Hours for Admin to review penalty appeal — §6.17", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 20, "appeal_auto_approve", "true", "Boolean", "Auto-approve appeal when Admin misses SLA — §6.17", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_payments_IdempotencyKey",
                table: "payments",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payments_PayerId",
                table: "payments",
                column: "PayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Lounges_AtmosphereId",
                table: "Lounges",
                column: "AtmosphereId");

            migrationBuilder.CreateIndex(
                name: "IX_livestream_ticket_details_LivestreamId",
                table: "livestream_ticket_details",
                column: "LivestreamId");

            migrationBuilder.AddForeignKey(
                name: "FK_livestream_ticket_details_livestreams_LivestreamId",
                table: "livestream_ticket_details",
                column: "LivestreamId",
                principalTable: "livestreams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Lounges_Atmospheres_AtmosphereId",
                table: "Lounges",
                column: "AtmosphereId",
                principalTable: "Atmospheres",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_Users_PayerId",
                table: "payments",
                column: "PayerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_livestream_ticket_details_livestreams_LivestreamId",
                table: "livestream_ticket_details");

            migrationBuilder.DropForeignKey(
                name: "FK_Lounges_Atmospheres_AtmosphereId",
                table: "Lounges");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_Users_PayerId",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payments_IdempotencyKey",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payments_PayerId",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_Lounges_AtmosphereId",
                table: "Lounges");

            migrationBuilder.DropIndex(
                name: "IX_livestream_ticket_details_LivestreamId",
                table: "livestream_ticket_details");

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DropColumn(
                name: "Description",
                table: "TicketPrices");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "TicketPrices");

            migrationBuilder.DropColumn(
                name: "Sold",
                table: "TicketPrices");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Performances");

            migrationBuilder.DropColumn(
                name: "SetTime",
                table: "Performances");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "Method",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "PayerId",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "SettlementStatus",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "AtmosphereId",
                table: "Lounges");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Lounges");

            migrationBuilder.DropColumn(
                name: "LivestreamId",
                table: "livestream_ticket_details");

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "TicketPrices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(15,2)",
                oldPrecision: 15,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "ReputationScore",
                table: "Lounges",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 100m,
                oldClrType: typeof(decimal),
                oldType: "decimal(3,2)",
                oldPrecision: 3,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 4,
                column: "Description",
                value: "Days before show end: release 70%");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 5,
                column: "Description",
                value: "Days after show end: release 30%");

            migrationBuilder.InsertData(
                table: "system_config",
                columns: new[] { "Id", "ConfigKey", "ConfigValue", "DataType", "Description", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "platform_commission_rate", "0.05", "Decimal", "Platform fee rate (5%) — NĐ 117/2025", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 2, "tax_rate", "0.05", "Decimal", "VAT withheld at source (5%) — NĐ 117/2025", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 3, "settlement_partial_pct", "0.70", "Decimal", "Stage-1 settlement ratio (70% released at T+48h)", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 6, "ai_auto_pass_threshold", "0.20", "Decimal", "AI moderation score below this → auto-pass — §6.11", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 7, "ai_auto_reject_threshold", "0.80", "Decimal", "AI moderation score above this → auto-reject — §6.11", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 8, "moderation_sla_hours", "24", "Integer", "Admin SLA to review flagged content — NĐ 147/2024", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 9, "ticket_hold_minutes", "15", "Integer", "Checkout hold duration before slot released — §6.3", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 10, "donation_hold_days", "7", "Integer", "Days before auto-confirm donation if Owner inactive — BR-05", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 11, "rating_window_days", "7", "Integer", "Days after show end to submit rating — §6.13", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 12, "appeal_sla_hours", "48", "Integer", "Hours for Admin to review penalty appeal — §6.17", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 13, "appeal_auto_approve", "true", "Boolean", "Auto-approve appeal when Admin misses SLA — §6.17", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null }
                });
        }
    }
}
