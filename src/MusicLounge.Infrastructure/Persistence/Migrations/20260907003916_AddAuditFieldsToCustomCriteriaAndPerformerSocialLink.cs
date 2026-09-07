using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFieldsToCustomCriteriaAndPerformerSocialLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "performer_social_links",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "performer_social_links",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "performer_social_links",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "performer_social_links",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "event_custom_values",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "event_custom_values",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "event_custom_values",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "event_custom_values",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "custom_criteria",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "custom_criteria",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "custom_criteria",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "custom_criteria",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "performer_social_links");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "performer_social_links");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "performer_social_links");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "performer_social_links");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "event_custom_values");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "event_custom_values");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "event_custom_values");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "event_custom_values");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "custom_criteria");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "custom_criteria");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "custom_criteria");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "custom_criteria",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");
        }
    }
}
