using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CCC1_AddCitizenCardFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CitizenCardBackImageUrl",
                table: "users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CitizenCardFrontImageUrl",
                table: "users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CitizenCardNumber",
                table: "users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CitizenCardSubmittedAt",
                table: "users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_CitizenCardNumber",
                table: "users",
                column: "CitizenCardNumber",
                unique: true,
                filter: "[CitizenCardNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_CitizenCardNumber",
                table: "users");

            migrationBuilder.DropColumn(
                name: "CitizenCardBackImageUrl",
                table: "users");

            migrationBuilder.DropColumn(
                name: "CitizenCardFrontImageUrl",
                table: "users");

            migrationBuilder.DropColumn(
                name: "CitizenCardNumber",
                table: "users");

            migrationBuilder.DropColumn(
                name: "CitizenCardSubmittedAt",
                table: "users");
        }
    }
}
