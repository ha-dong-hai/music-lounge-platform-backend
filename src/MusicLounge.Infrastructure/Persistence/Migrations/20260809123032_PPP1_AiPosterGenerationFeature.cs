using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PPP1_AiPosterGenerationFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxAiPostersPerMonth",
                table: "subscription_packages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SubscriptionMaxAiPostersPerMonthSnapshot",
                table: "payments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAiPostersPerMonthSnapshot",
                table: "owner_subscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ai_poster_generations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShowId = table.Column<int>(type: "int", nullable: false),
                    OwnerId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_poster_generations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_poster_generations_lounge_shows_ShowId",
                        column: x => x.ShowId,
                        principalTable: "lounge_shows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_poster_generations_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "system_config",
                columns: new[] { "Id", "ConfigKey", "ConfigValue", "DataType", "Description", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 31, "ai_poster_max_attempts_per_show", "5", "Integer", "Max AI poster generation attempts (incl. failures) per show", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });

            migrationBuilder.CreateIndex(
                name: "IX_ai_poster_generations_OwnerId_CreatedAt",
                table: "ai_poster_generations",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_poster_generations_ShowId_CreatedAt",
                table: "ai_poster_generations",
                columns: new[] { "ShowId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_poster_generations");

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DropColumn(
                name: "MaxAiPostersPerMonth",
                table: "subscription_packages");

            migrationBuilder.DropColumn(
                name: "SubscriptionMaxAiPostersPerMonthSnapshot",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "MaxAiPostersPerMonthSnapshot",
                table: "owner_subscriptions");
        }
    }
}
