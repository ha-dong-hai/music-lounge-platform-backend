using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLACP489_NotificationEnglishAndUserLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "users",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "vi");

            migrationBuilder.AddColumn<string>(
                name: "BodyEn",
                table: "notifications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleEn",
                table: "notifications",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "users");

            migrationBuilder.DropColumn(
                name: "BodyEn",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "TitleEn",
                table: "notifications");
        }
    }
}
