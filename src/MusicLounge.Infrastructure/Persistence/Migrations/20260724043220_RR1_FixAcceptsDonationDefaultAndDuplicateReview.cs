using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RR1_FixAcceptsDonationDefaultAndDuplicateReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_complaints_users_AdminId",
                table: "complaints");

            migrationBuilder.DropForeignKey(
                name: "FK_event_custom_values_custom_criteria_CriteriaId",
                table: "event_custom_values");

            migrationBuilder.DropForeignKey(
                name: "FK_fnb_orders_seating_zones_ZoneId",
                table: "fnb_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_fnb_orders_users_StaffId",
                table: "fnb_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_refund_requests_users_ProcessedBy",
                table: "refund_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_ticket_tiers_seating_zones_ZoneId",
                table: "ticket_tiers");

            migrationBuilder.AlterColumn<bool>(
                name: "AcceptsDonation",
                table: "performances",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AddForeignKey(
                name: "FK_complaints_users_AdminId",
                table: "complaints",
                column: "AdminId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_event_custom_values_custom_criteria_CriteriaId",
                table: "event_custom_values",
                column: "CriteriaId",
                principalTable: "custom_criteria",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_fnb_orders_seating_zones_ZoneId",
                table: "fnb_orders",
                column: "ZoneId",
                principalTable: "seating_zones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_fnb_orders_users_StaffId",
                table: "fnb_orders",
                column: "StaffId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_refund_requests_users_ProcessedBy",
                table: "refund_requests",
                column: "ProcessedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ticket_tiers_seating_zones_ZoneId",
                table: "ticket_tiers",
                column: "ZoneId",
                principalTable: "seating_zones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_complaints_users_AdminId",
                table: "complaints");

            migrationBuilder.DropForeignKey(
                name: "FK_event_custom_values_custom_criteria_CriteriaId",
                table: "event_custom_values");

            migrationBuilder.DropForeignKey(
                name: "FK_fnb_orders_seating_zones_ZoneId",
                table: "fnb_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_fnb_orders_users_StaffId",
                table: "fnb_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_refund_requests_users_ProcessedBy",
                table: "refund_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_ticket_tiers_seating_zones_ZoneId",
                table: "ticket_tiers");

            migrationBuilder.AlterColumn<bool>(
                name: "AcceptsDonation",
                table: "performances",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AddForeignKey(
                name: "FK_complaints_users_AdminId",
                table: "complaints",
                column: "AdminId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_event_custom_values_custom_criteria_CriteriaId",
                table: "event_custom_values",
                column: "CriteriaId",
                principalTable: "custom_criteria",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_fnb_orders_seating_zones_ZoneId",
                table: "fnb_orders",
                column: "ZoneId",
                principalTable: "seating_zones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_fnb_orders_users_StaffId",
                table: "fnb_orders",
                column: "StaffId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_refund_requests_users_ProcessedBy",
                table: "refund_requests",
                column: "ProcessedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ticket_tiers_seating_zones_ZoneId",
                table: "ticket_tiers",
                column: "ZoneId",
                principalTable: "seating_zones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
