using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LLL1_PhoneVerificationErasureModerationSlaDonationConfigAndFixedStaffFks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DataErasedAt",
                table: "users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PhoneVerificationCodeExpiresAt",
                table: "users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneVerificationCodeHash",
                table: "users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SlaDeadline",
                table: "event_moderations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.InsertData(
                table: "system_config",
                columns: new[] { "Id", "ConfigKey", "ConfigValue", "DataType", "Description", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 21, "donation_performer_share_rate", "0.88", "Decimal", "§6.5 chặng 2: % of gross donation forwarded to performer", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });

            migrationBuilder.CreateIndex(
                name: "IX_physical_ticket_details_CheckedInByStaffId",
                table: "physical_ticket_details",
                column: "CheckedInByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_physical_ticket_details_SoldByStaffId",
                table: "physical_ticket_details",
                column: "SoldByStaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_physical_ticket_details_users_CheckedInByStaffId",
                table: "physical_ticket_details",
                column: "CheckedInByStaffId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_physical_ticket_details_users_SoldByStaffId",
                table: "physical_ticket_details",
                column: "SoldByStaffId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_physical_ticket_details_users_CheckedInByStaffId",
                table: "physical_ticket_details");

            migrationBuilder.DropForeignKey(
                name: "FK_physical_ticket_details_users_SoldByStaffId",
                table: "physical_ticket_details");

            migrationBuilder.DropIndex(
                name: "IX_physical_ticket_details_CheckedInByStaffId",
                table: "physical_ticket_details");

            migrationBuilder.DropIndex(
                name: "IX_physical_ticket_details_SoldByStaffId",
                table: "physical_ticket_details");

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DropColumn(
                name: "DataErasedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PhoneVerificationCodeExpiresAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PhoneVerificationCodeHash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "SlaDeadline",
                table: "event_moderations");
        }
    }
}
