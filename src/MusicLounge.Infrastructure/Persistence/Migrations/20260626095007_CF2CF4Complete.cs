using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CF2CF4Complete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TerminatedById",
                table: "livestreams",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminatedReason",
                table: "livestreams",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "donations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DonorUserId = table.Column<int>(type: "int", nullable: true),
                    PerformanceId = table.Column<int>(type: "int", nullable: false),
                    Gross = table.Column<decimal>(type: "decimal(15,2)", nullable: false),
                    Net = table.Column<decimal>(type: "decimal(15,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AutoConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    OwnerAckAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OwnerPaidAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PaymentRef = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PaymentEvidenceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsAnonymous = table.Column<bool>(type: "bit", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsAmountPublic = table.Column<bool>(type: "bit", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsMessagePublic = table.Column<bool>(type: "bit", nullable: false),
                    GatewayRef = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_donations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_donations_Performances_PerformanceId",
                        column: x => x.PerformanceId,
                        principalTable: "Performances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_donations_Users_DonorUserId",
                        column: x => x.DonorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "event_moderations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TargetType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TargetId = table.Column<int>(type: "int", nullable: false),
                    AiScore = table.Column<float>(type: "real", nullable: true),
                    RiskLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FlagReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AiRecommendation = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AdminId = table.Column<int>(type: "int", nullable: true),
                    AdminDecision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_moderations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_moderations_Users_AdminId",
                        column: x => x.AdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_livestreams_TerminatedById",
                table: "livestreams",
                column: "TerminatedById");

            migrationBuilder.CreateIndex(
                name: "IX_donations_DonorUserId",
                table: "donations",
                column: "DonorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_donations_PerformanceId",
                table: "donations",
                column: "PerformanceId");

            migrationBuilder.CreateIndex(
                name: "IX_donations_Status",
                table: "donations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_event_moderations_AdminDecision",
                table: "event_moderations",
                column: "AdminDecision");

            migrationBuilder.CreateIndex(
                name: "IX_event_moderations_AdminId",
                table: "event_moderations",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_event_moderations_TargetType_TargetId",
                table: "event_moderations",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.AddForeignKey(
                name: "FK_livestreams_Users_TerminatedById",
                table: "livestreams",
                column: "TerminatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_livestreams_Users_TerminatedById",
                table: "livestreams");

            migrationBuilder.DropTable(
                name: "donations");

            migrationBuilder.DropTable(
                name: "event_moderations");

            migrationBuilder.DropIndex(
                name: "IX_livestreams_TerminatedById",
                table: "livestreams");

            migrationBuilder.DropColumn(
                name: "TerminatedById",
                table: "livestreams");

            migrationBuilder.DropColumn(
                name: "TerminatedReason",
                table: "livestreams");
        }
    }
}
