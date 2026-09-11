using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLACP364_PerformerSelfConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "performers",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DataConsentAt",
                table: "performers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "performer_confirmations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PerformerId = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BankAccountId = table.Column<int>(type: "int", nullable: true),
                    BankAccountFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DonationId = table.Column<int>(type: "int", nullable: true),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SentToEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ConsentGivenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_performer_confirmations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_performer_confirmations_bank_accounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "bank_accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_performer_confirmations_donations_DonationId",
                        column: x => x.DonationId,
                        principalTable: "donations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_performer_confirmations_performers_PerformerId",
                        column: x => x.PerformerId,
                        principalTable: "performers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_performer_confirmations_BankAccountId",
                table: "performer_confirmations",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_performer_confirmations_DonationId",
                table: "performer_confirmations",
                column: "DonationId");

            migrationBuilder.CreateIndex(
                name: "IX_performer_confirmations_PerformerId",
                table: "performer_confirmations",
                column: "PerformerId");

            migrationBuilder.CreateIndex(
                name: "IX_performer_confirmations_TokenHash",
                table: "performer_confirmations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "performer_confirmations");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "performers");

            migrationBuilder.DropColumn(
                name: "DataConsentAt",
                table: "performers");
        }
    }
}
