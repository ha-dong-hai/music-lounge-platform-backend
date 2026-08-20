using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VV1_LegalApprovalForShows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LegalApprovalConfirmedAt",
                table: "lounge_shows",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegalApprovalConfirmedByAdminId",
                table: "lounge_shows",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalApprovalReference",
                table: "lounge_shows",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_lounge_shows_LegalApprovalConfirmedByAdminId",
                table: "lounge_shows",
                column: "LegalApprovalConfirmedByAdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_lounge_shows_users_LegalApprovalConfirmedByAdminId",
                table: "lounge_shows",
                column: "LegalApprovalConfirmedByAdminId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_lounge_shows_users_LegalApprovalConfirmedByAdminId",
                table: "lounge_shows");

            migrationBuilder.DropIndex(
                name: "IX_lounge_shows_LegalApprovalConfirmedByAdminId",
                table: "lounge_shows");

            migrationBuilder.DropColumn(
                name: "LegalApprovalConfirmedAt",
                table: "lounge_shows");

            migrationBuilder.DropColumn(
                name: "LegalApprovalConfirmedByAdminId",
                table: "lounge_shows");

            migrationBuilder.DropColumn(
                name: "LegalApprovalReference",
                table: "lounge_shows");
        }
    }
}
