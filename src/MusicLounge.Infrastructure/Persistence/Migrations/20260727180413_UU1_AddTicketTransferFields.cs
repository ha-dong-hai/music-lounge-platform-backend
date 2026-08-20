using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UU1_AddTicketTransferFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PendingTransferInitiatedAt",
                table: "tickets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingTransferToUserId",
                table: "tickets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tickets_PendingTransferToUserId",
                table: "tickets",
                column: "PendingTransferToUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_tickets_users_PendingTransferToUserId",
                table: "tickets",
                column: "PendingTransferToUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tickets_users_PendingTransferToUserId",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "IX_tickets_PendingTransferToUserId",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "PendingTransferInitiatedAt",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "PendingTransferToUserId",
                table: "tickets");
        }
    }
}
