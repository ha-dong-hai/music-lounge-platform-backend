using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLACP377_OwnerLoungeUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_music_lounges_OwnerId",
                table: "music_lounges");

            migrationBuilder.CreateIndex(
                name: "IX_music_lounges_OwnerId",
                table: "music_lounges",
                column: "OwnerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_music_lounges_OwnerId",
                table: "music_lounges");

            migrationBuilder.CreateIndex(
                name: "IX_music_lounges_OwnerId",
                table: "music_lounges",
                column: "OwnerId");
        }
    }
}
