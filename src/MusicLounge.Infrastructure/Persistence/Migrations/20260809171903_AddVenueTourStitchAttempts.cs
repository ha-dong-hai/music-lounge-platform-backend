using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVenueTourStitchAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "venue_tour_stitch_attempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoungeId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ResultSceneId = table.Column<int>(type: "int", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venue_tour_stitch_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_venue_tour_stitch_attempts_music_lounges_LoungeId",
                        column: x => x.LoungeId,
                        principalTable: "music_lounges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_venue_tour_stitch_attempts_venue_tour_scenes_ResultSceneId",
                        column: x => x.ResultSceneId,
                        principalTable: "venue_tour_scenes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_venue_tour_stitch_attempts_LoungeId",
                table: "venue_tour_stitch_attempts",
                column: "LoungeId");

            migrationBuilder.CreateIndex(
                name: "IX_venue_tour_stitch_attempts_ResultSceneId",
                table: "venue_tour_stitch_attempts",
                column: "ResultSceneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "venue_tour_stitch_attempts");
        }
    }
}
