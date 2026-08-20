using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NN1_SchemaGapFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ledger_entries_ledger_accounts_AccountId",
                table: "ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_AccountId",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "ledger_entries");

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRemoved",
                table: "Ratings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RemovedReason",
                table: "Ratings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AreaLayoutImageUrl",
                table: "Lounges",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessLicenseUrl",
                table: "Lounges",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReputationScore",
                table: "Lounges",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 100m);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Lounges",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.CreateIndex(
                name: "IX_Lounges_Status",
                table: "Lounges",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Lounges_Status",
                table: "Lounges");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsRemoved",
                table: "Ratings");

            migrationBuilder.DropColumn(
                name: "RemovedReason",
                table: "Ratings");

            migrationBuilder.DropColumn(
                name: "AreaLayoutImageUrl",
                table: "Lounges");

            migrationBuilder.DropColumn(
                name: "BusinessLicenseUrl",
                table: "Lounges");

            migrationBuilder.DropColumn(
                name: "ReputationScore",
                table: "Lounges");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Lounges");

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "ledger_entries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_AccountId",
                table: "ledger_entries",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_ledger_entries_ledger_accounts_AccountId",
                table: "ledger_entries",
                column: "AccountId",
                principalTable: "ledger_accounts",
                principalColumn: "Id");
        }
    }
}
