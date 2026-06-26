using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCitizenCardFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "citizen_card_back_image_url",
                table: "users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "citizen_card_front_image_url",
                table: "users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "citizen_card_number",
                table: "users",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "citizen_card_storage_provider",
                table: "users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "citizen_card_updated_at",
                table: "users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "i_x_users_citizen_card_number",
                table: "users",
                column: "citizen_card_number");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_users_citizen_card_number",
                table: "users");

            migrationBuilder.DropColumn(
                name: "citizen_card_back_image_url",
                table: "users");

            migrationBuilder.DropColumn(
                name: "citizen_card_front_image_url",
                table: "users");

            migrationBuilder.DropColumn(
                name: "citizen_card_number",
                table: "users");

            migrationBuilder.DropColumn(
                name: "citizen_card_storage_provider",
                table: "users");

            migrationBuilder.DropColumn(
                name: "citizen_card_updated_at",
                table: "users");
        }
    }
}
