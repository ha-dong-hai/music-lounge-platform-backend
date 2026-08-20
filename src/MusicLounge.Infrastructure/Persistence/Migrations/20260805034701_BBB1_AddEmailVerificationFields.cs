using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BBB1_AddEmailVerificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailVerificationCodeExpiresAt",
                table: "users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailVerificationCodeHash",
                table: "users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailVerifiedAt",
                table: "users",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailVerificationCodeExpiresAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmailVerificationCodeHash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmailVerifiedAt",
                table: "users");
        }
    }
}
