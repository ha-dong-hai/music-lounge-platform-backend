using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLACP290_KycReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CitizenCardReviewNote",
                table: "users",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CitizenCardReviewStatus",
                table: "users",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CitizenCardReviewedAt",
                table: "users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CitizenCardReviewedBy",
                table: "users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxProfileReviewNote",
                table: "users",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxProfileReviewStatus",
                table: "users",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CitizenCardReviewNote",
                table: "users");

            migrationBuilder.DropColumn(
                name: "CitizenCardReviewStatus",
                table: "users");

            migrationBuilder.DropColumn(
                name: "CitizenCardReviewedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "CitizenCardReviewedBy",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TaxProfileReviewNote",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TaxProfileReviewStatus",
                table: "users");
        }
    }
}
