using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLACP574_RatingAiModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiFlagReason",
                table: "lounge_show_ratings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiRiskLevel",
                table: "lounge_show_ratings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "AiScore",
                table: "lounge_show_ratings",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CommentHiddenAt",
                table: "lounge_show_ratings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ReporterId",
                table: "content_reports",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiFlagReason",
                table: "lounge_show_ratings");

            migrationBuilder.DropColumn(
                name: "AiRiskLevel",
                table: "lounge_show_ratings");

            migrationBuilder.DropColumn(
                name: "AiScore",
                table: "lounge_show_ratings");

            migrationBuilder.DropColumn(
                name: "CommentHiddenAt",
                table: "lounge_show_ratings");

            migrationBuilder.AlterColumn<Guid>(
                name: "ReporterId",
                table: "content_reports",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
