using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HHH1_EncryptCitizenCardNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_CitizenCardNumber",
                table: "users");

            migrationBuilder.AlterColumn<string>(
                name: "CitizenCardNumber",
                table: "users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CitizenCardNumberHash",
                table: "users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_CitizenCardNumberHash",
                table: "users",
                column: "CitizenCardNumberHash",
                unique: true,
                filter: "[CitizenCardNumberHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_CitizenCardNumberHash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "CitizenCardNumberHash",
                table: "users");

            migrationBuilder.AlterColumn<string>(
                name: "CitizenCardNumber",
                table: "users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_CitizenCardNumber",
                table: "users",
                column: "CitizenCardNumber",
                unique: true,
                filter: "[CitizenCardNumber] IS NOT NULL");
        }
    }
}
