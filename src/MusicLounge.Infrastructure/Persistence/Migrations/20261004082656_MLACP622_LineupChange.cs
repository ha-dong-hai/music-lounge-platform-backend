using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLACP622_LineupChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LineupChangeNote",
                table: "lounge_shows",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LineupChangedAt",
                table: "lounge_shows",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LineupChangeNote",
                table: "lounge_shows");

            migrationBuilder.DropColumn(
                name: "LineupChangedAt",
                table: "lounge_shows");
        }
    }
}
