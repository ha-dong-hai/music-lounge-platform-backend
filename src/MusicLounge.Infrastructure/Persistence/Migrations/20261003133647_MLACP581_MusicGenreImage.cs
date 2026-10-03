using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLACP581_MusicGenreImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "music_genres",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "music_genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0001-81b5-8b05-000000000001"),
                column: "ImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "music_genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0002-81b5-8b05-000000000002"),
                column: "ImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "music_genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0003-81b5-8b05-000000000003"),
                column: "ImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "music_genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0004-81b5-8b05-000000000004"),
                column: "ImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "music_genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0005-81b5-8b05-000000000005"),
                column: "ImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "music_genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0006-81b5-8b05-000000000006"),
                column: "ImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "music_genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0007-81b5-8b05-000000000007"),
                column: "ImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "music_genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0008-81b5-8b05-000000000008"),
                column: "ImageUrl",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "music_genres");
        }
    }
}
