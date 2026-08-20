using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class XX1_Add3DTourAndPlaybackMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Model3DUrl",
                table: "music_lounges",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlaybackMode",
                table: "lounge_shows",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "TwoD");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Model3DUrl",
                table: "music_lounges");

            migrationBuilder.DropColumn(
                name: "PlaybackMode",
                table: "lounge_shows");
        }
    }
}
