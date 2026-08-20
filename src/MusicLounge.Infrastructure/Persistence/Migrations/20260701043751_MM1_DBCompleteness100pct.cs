using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MM1_DBCompleteness100pct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_settlements_OwnerId_Stage_Status",
                table: "settlements");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "settlements");

            migrationBuilder.DropColumn(
                name: "BankAccountSnapshot",
                table: "donations");

            migrationBuilder.RenameColumn(
                name: "PlatformFeeRate",
                table: "settlements",
                newName: "PreRateApplied");

            migrationBuilder.RenameColumn(
                name: "PaidAt",
                table: "settlements",
                newName: "ReleasedAt");

            migrationBuilder.RenameColumn(
                name: "WatchedAt",
                table: "livestream_ticket_details",
                newName: "LastAccessedAt");

            migrationBuilder.RenameColumn(
                name: "HlsUrl",
                table: "livestream_ticket_details",
                newName: "AccessToken");

            migrationBuilder.AlterColumn<decimal>(
                name: "NetAmount",
                table: "settlements",
                type: "decimal(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "GrossAmount",
                table: "settlements",
                type: "decimal(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<int>(
                name: "BankAccountId",
                table: "settlements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LedgerJournalId",
                table: "settlements",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PostRateApplied",
                table: "settlements",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ReleaseType",
                table: "settlements",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "CancellationAllowed",
                table: "LoungeShows",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "CancellationDeadlineHours",
                table: "LoungeShows",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "LoungeShows",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PosterByAi",
                table: "LoungeShows",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundPercentage",
                table: "LoungeShows",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TicketSaleClosesAt",
                table: "LoungeShows",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FirstAccessedAt",
                table: "livestream_ticket_details",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BankAccountId",
                table: "donations",
                type: "int",
                nullable: true);

            migrationBuilder.InsertData(
                table: "system_config",
                columns: new[] { "Id", "ConfigKey", "ConfigValue", "DataType", "Description", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "platform_commission_rate", "0.05", "Decimal", "Platform fee rate (5%) — NĐ 117/2025", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 2, "tax_rate", "0.05", "Decimal", "VAT withheld at source (5%) — NĐ 117/2025", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 3, "settlement_partial_pct", "0.70", "Decimal", "Stage-1 settlement ratio (70% released at T+48h)", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 4, "settlement_days_before", "3", "Integer", "Days before show end: release 70%", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 5, "settlement_days_after", "3", "Integer", "Days after show end: release 30%", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 6, "ai_auto_pass_threshold", "0.20", "Decimal", "AI moderation score below this → auto-pass — §6.11", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 7, "ai_auto_reject_threshold", "0.80", "Decimal", "AI moderation score above this → auto-reject — §6.11", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 8, "moderation_sla_hours", "24", "Integer", "Admin SLA to review flagged content — NĐ 147/2024", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 9, "ticket_hold_minutes", "15", "Integer", "Checkout hold duration before slot released — §6.3", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 10, "donation_hold_days", "7", "Integer", "Days before auto-confirm donation if Owner inactive — BR-05", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 11, "rating_window_days", "7", "Integer", "Days after show end to submit rating — §6.13", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 12, "appeal_sla_hours", "48", "Integer", "Hours for Admin to review penalty appeal — §6.17", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 13, "appeal_auto_approve", "true", "Boolean", "Auto-approve appeal when Admin misses SLA — §6.17", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_settlements_BankAccountId",
                table: "settlements",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_settlements_OwnerId_ReleaseType_Status",
                table: "settlements",
                columns: new[] { "OwnerId", "ReleaseType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_livestream_ticket_details_AccessToken",
                table: "livestream_ticket_details",
                column: "AccessToken",
                unique: true,
                filter: "[AccessToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_donations_BankAccountId",
                table: "donations",
                column: "BankAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_donations_bank_accounts_BankAccountId",
                table: "donations",
                column: "BankAccountId",
                principalTable: "bank_accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_settlements_bank_accounts_BankAccountId",
                table: "settlements",
                column: "BankAccountId",
                principalTable: "bank_accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_donations_bank_accounts_BankAccountId",
                table: "donations");

            migrationBuilder.DropForeignKey(
                name: "FK_settlements_bank_accounts_BankAccountId",
                table: "settlements");

            migrationBuilder.DropIndex(
                name: "IX_settlements_BankAccountId",
                table: "settlements");

            migrationBuilder.DropIndex(
                name: "IX_settlements_OwnerId_ReleaseType_Status",
                table: "settlements");

            migrationBuilder.DropIndex(
                name: "IX_livestream_ticket_details_AccessToken",
                table: "livestream_ticket_details");

            migrationBuilder.DropIndex(
                name: "IX_donations_BankAccountId",
                table: "donations");

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
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 5);

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

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "settlements");

            migrationBuilder.DropColumn(
                name: "LedgerJournalId",
                table: "settlements");

            migrationBuilder.DropColumn(
                name: "PostRateApplied",
                table: "settlements");

            migrationBuilder.DropColumn(
                name: "ReleaseType",
                table: "settlements");

            migrationBuilder.DropColumn(
                name: "CancellationAllowed",
                table: "LoungeShows");

            migrationBuilder.DropColumn(
                name: "CancellationDeadlineHours",
                table: "LoungeShows");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "LoungeShows");

            migrationBuilder.DropColumn(
                name: "PosterByAi",
                table: "LoungeShows");

            migrationBuilder.DropColumn(
                name: "RefundPercentage",
                table: "LoungeShows");

            migrationBuilder.DropColumn(
                name: "TicketSaleClosesAt",
                table: "LoungeShows");

            migrationBuilder.DropColumn(
                name: "FirstAccessedAt",
                table: "livestream_ticket_details");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "donations");

            migrationBuilder.RenameColumn(
                name: "ReleasedAt",
                table: "settlements",
                newName: "PaidAt");

            migrationBuilder.RenameColumn(
                name: "PreRateApplied",
                table: "settlements",
                newName: "PlatformFeeRate");

            migrationBuilder.RenameColumn(
                name: "LastAccessedAt",
                table: "livestream_ticket_details",
                newName: "WatchedAt");

            migrationBuilder.RenameColumn(
                name: "AccessToken",
                table: "livestream_ticket_details",
                newName: "HlsUrl");

            migrationBuilder.AlterColumn<decimal>(
                name: "NetAmount",
                table: "settlements",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(15,2)",
                oldPrecision: 15,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "GrossAmount",
                table: "settlements",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(15,2)",
                oldPrecision: 15,
                oldScale: 2);

            migrationBuilder.AddColumn<int>(
                name: "Stage",
                table: "settlements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountSnapshot",
                table: "donations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_settlements_OwnerId_Stage_Status",
                table: "settlements",
                columns: new[] { "OwnerId", "Stage", "Status" });
        }
    }
}
