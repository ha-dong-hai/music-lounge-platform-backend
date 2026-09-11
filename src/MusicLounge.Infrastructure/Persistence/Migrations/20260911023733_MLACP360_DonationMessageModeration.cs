using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLACP360_DonationMessageModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MessageHiddenAt",
                table: "donations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MessageHiddenByUserId",
                table: "donations",
                type: "int",
                nullable: true);

            migrationBuilder.InsertData(
                table: "system_config",
                columns: new[] { "Id", "ConfigKey", "ConfigValue", "DataType", "Description", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 35, "donation_message_blocked_words", "[]", "Json", "JSON array of words/phrases that keep a donation message off the livestream alert (whole-word, case- and diacritic-insensitive). The donation itself is still announced (MLACP-360)", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });

            migrationBuilder.CreateIndex(
                name: "IX_donations_MessageHiddenByUserId",
                table: "donations",
                column: "MessageHiddenByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_donations_users_MessageHiddenByUserId",
                table: "donations",
                column: "MessageHiddenByUserId",
                principalTable: "users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_donations_users_MessageHiddenByUserId",
                table: "donations");

            migrationBuilder.DropIndex(
                name: "IX_donations_MessageHiddenByUserId",
                table: "donations");

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DropColumn(
                name: "MessageHiddenAt",
                table: "donations");

            migrationBuilder.DropColumn(
                name: "MessageHiddenByUserId",
                table: "donations");
        }
    }
}
