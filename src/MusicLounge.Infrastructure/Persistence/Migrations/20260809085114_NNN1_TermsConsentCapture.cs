using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NNN1_TermsConsentCapture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TermsAcceptedAt",
                table: "users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsVersion",
                table: "users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.InsertData(
                table: "system_config",
                columns: new[] { "Id", "ConfigKey", "ConfigValue", "DataType", "Description", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 23, "current_terms_version", "v0-placeholder-pending-legal-review", "String", "Version label of the currently-published ToS/Privacy Policy — Luật 91/2025/QH15 lawful-basis consent", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DropColumn(
                name: "TermsAcceptedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TermsVersion",
                table: "users");
        }
    }
}
