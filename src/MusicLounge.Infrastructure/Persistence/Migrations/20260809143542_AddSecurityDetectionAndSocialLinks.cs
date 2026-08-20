using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityDetectionAndSocialLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "known_admin_snapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    FirstDetectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_known_admin_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "login_failure_logs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_failure_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "login_spike_alert_states",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LastAlertedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_spike_alert_states", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "performer_social_links",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PerformerId = table.Column<int>(type: "int", nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_performer_social_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_performer_social_links_performers_PerformerId",
                        column: x => x.PerformerId,
                        principalTable: "performers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_known_admin_snapshots_UserId",
                table: "known_admin_snapshots",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_login_failure_logs_CreatedAt",
                table: "login_failure_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_login_failure_logs_IpAddress",
                table: "login_failure_logs",
                column: "IpAddress");

            migrationBuilder.CreateIndex(
                name: "IX_login_spike_alert_states_IpAddress",
                table: "login_spike_alert_states",
                column: "IpAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_performer_social_links_PerformerId_Platform",
                table: "performer_social_links",
                columns: new[] { "PerformerId", "Platform" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "known_admin_snapshots");

            migrationBuilder.DropTable(
                name: "login_failure_logs");

            migrationBuilder.DropTable(
                name: "login_spike_alert_states");

            migrationBuilder.DropTable(
                name: "performer_social_links");
        }
    }
}
