using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLACP387_RefundPayoutAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayoutAccountHolder",
                table: "refund_requests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutAccountNumber",
                table: "refund_requests",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PayoutAccountRequestedAt",
                table: "refund_requests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutBankName",
                table: "refund_requests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PayoutConsentAt",
                table: "refund_requests",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayoutAccountHolder",
                table: "refund_requests");

            migrationBuilder.DropColumn(
                name: "PayoutAccountNumber",
                table: "refund_requests");

            migrationBuilder.DropColumn(
                name: "PayoutAccountRequestedAt",
                table: "refund_requests");

            migrationBuilder.DropColumn(
                name: "PayoutBankName",
                table: "refund_requests");

            migrationBuilder.DropColumn(
                name: "PayoutConsentAt",
                table: "refund_requests");
        }
    }
}
