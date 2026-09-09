using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLACP330_NegativePreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lounge_mutes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LoungeId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lounge_mutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lounge_mutes_music_lounges_LoungeId",
                        column: x => x.LoungeId,
                        principalTable: "music_lounges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_lounge_mutes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_disliked_genres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GenreId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_disliked_genres", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_disliked_genres_music_genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "music_genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_disliked_genres_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lounge_mutes_LoungeId",
                table: "lounge_mutes",
                column: "LoungeId");

            migrationBuilder.CreateIndex(
                name: "IX_lounge_mutes_UserId_LoungeId",
                table: "lounge_mutes",
                columns: new[] { "UserId", "LoungeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_disliked_genres_GenreId",
                table: "user_disliked_genres",
                column: "GenreId");

            migrationBuilder.CreateIndex(
                name: "IX_user_disliked_genres_UserId_GenreId",
                table: "user_disliked_genres",
                columns: new[] { "UserId", "GenreId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lounge_mutes");

            migrationBuilder.DropTable(
                name: "user_disliked_genres");
        }
    }
}
