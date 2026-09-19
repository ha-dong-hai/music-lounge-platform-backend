using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLACP458_PosterJobQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "ai_poster_generations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClaimedAt",
                table: "ai_poster_generations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaimedBy",
                table: "ai_poster_generations",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseExpiresAt",
                table: "ai_poster_generations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "ai_poster_generations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_poster_generations_Status_CreatedAt",
                table: "ai_poster_generations",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ai_poster_generations_Status_CreatedAt",
                table: "ai_poster_generations");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "ai_poster_generations");

            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                table: "ai_poster_generations");

            migrationBuilder.DropColumn(
                name: "ClaimedBy",
                table: "ai_poster_generations");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                table: "ai_poster_generations");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "ai_poster_generations");
        }
    }
}
