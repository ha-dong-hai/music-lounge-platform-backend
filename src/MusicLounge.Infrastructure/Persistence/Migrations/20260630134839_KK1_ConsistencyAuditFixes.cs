using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KK1_ConsistencyAuditFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Follows_Performers_PerformerId",
                table: "Follows");

            migrationBuilder.DropForeignKey(
                name: "FK_Ratings_Users_UserId",
                table: "Ratings");

            migrationBuilder.DropIndex(
                name: "IX_ticket_holds_PriceId_ExpiresAt",
                table: "ticket_holds");

            migrationBuilder.DropIndex(
                name: "IX_settlements_OwnerId_Status",
                table: "settlements");

            migrationBuilder.DropIndex(
                name: "IX_Ratings_UserId_LoungeShowId",
                table: "Ratings");

            migrationBuilder.DropIndex(
                name: "IX_Follows_PerformerId",
                table: "Follows");

            migrationBuilder.DropColumn(
                name: "StreamKey",
                table: "livestream_ticket_details");

            migrationBuilder.DropColumn(
                name: "PerformerId",
                table: "Follows");

            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "settlements",
                newName: "NetAmount");

            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "payments",
                newName: "GrossAmount");

            migrationBuilder.RenameColumn(
                name: "Quota",
                table: "LoungeShows",
                newName: "OnlineQuota");

            migrationBuilder.RenameColumn(
                name: "StreamUrl",
                table: "livestream_ticket_details",
                newName: "HlsUrl");

            migrationBuilder.AddColumn<string>(
                name: "AuthProvider",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "local");

            migrationBuilder.AddColumn<string>(
                name: "GoogleId",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsReleased",
                table: "ticket_holds",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReleasedAt",
                table: "ticket_holds",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossAmount",
                table: "settlements",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeeRate",
                table: "settlements",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Stage",
                table: "settlements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Ratings",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "SoldByStaffId",
                table: "physical_ticket_details",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "Performers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Performers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "AcceptsDonation",
                table: "Performances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "GatewayFee",
                table: "payments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetAmount",
                table: "payments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFee",
                table: "payments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxWithheld",
                table: "payments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "OfflineQuota",
                table: "LoungeShows",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PosterUrl",
                table: "LoungeShows",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ChatEnabled",
                table: "livestreams",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFree",
                table: "livestreams",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "PeakViewerCount",
                table: "livestreams",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RecordingUrl",
                table: "livestreams",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalViews",
                table: "livestreams",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountSnapshot",
                table: "donations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaymentConfirmedAt",
                table: "donations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "BehaviourLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "BehaviourLogs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Algorithm",
                table: "AiRecommendations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Users_GoogleId",
                table: "Users",
                column: "GoogleId",
                unique: true,
                filter: "[GoogleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_holds_PriceId_IsReleased_ExpiresAt",
                table: "ticket_holds",
                columns: new[] { "PriceId", "IsReleased", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_settlements_OwnerId_Stage_Status",
                table: "settlements",
                columns: new[] { "OwnerId", "Stage", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_UserId_LoungeShowId",
                table: "Ratings",
                columns: new[] { "UserId", "LoungeShowId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Performers_CreatedByUserId",
                table: "Performers",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Performers_Users_CreatedByUserId",
                table: "Performers",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Ratings_Users_UserId",
                table: "Ratings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Performers_Users_CreatedByUserId",
                table: "Performers");

            migrationBuilder.DropForeignKey(
                name: "FK_Ratings_Users_UserId",
                table: "Ratings");

            migrationBuilder.DropIndex(
                name: "IX_Users_GoogleId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_ticket_holds_PriceId_IsReleased_ExpiresAt",
                table: "ticket_holds");

            migrationBuilder.DropIndex(
                name: "IX_settlements_OwnerId_Stage_Status",
                table: "settlements");

            migrationBuilder.DropIndex(
                name: "IX_Ratings_UserId_LoungeShowId",
                table: "Ratings");

            migrationBuilder.DropIndex(
                name: "IX_Performers_CreatedByUserId",
                table: "Performers");

            migrationBuilder.DropColumn(
                name: "AuthProvider",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GoogleId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsReleased",
                table: "ticket_holds");

            migrationBuilder.DropColumn(
                name: "ReleasedAt",
                table: "ticket_holds");

            migrationBuilder.DropColumn(
                name: "GrossAmount",
                table: "settlements");

            migrationBuilder.DropColumn(
                name: "PlatformFeeRate",
                table: "settlements");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "settlements");

            migrationBuilder.DropColumn(
                name: "SoldByStaffId",
                table: "physical_ticket_details");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Performers");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Performers");

            migrationBuilder.DropColumn(
                name: "AcceptsDonation",
                table: "Performances");

            migrationBuilder.DropColumn(
                name: "GatewayFee",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "NetAmount",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "PlatformFee",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "TaxWithheld",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "OfflineQuota",
                table: "LoungeShows");

            migrationBuilder.DropColumn(
                name: "PosterUrl",
                table: "LoungeShows");

            migrationBuilder.DropColumn(
                name: "ChatEnabled",
                table: "livestreams");

            migrationBuilder.DropColumn(
                name: "IsFree",
                table: "livestreams");

            migrationBuilder.DropColumn(
                name: "PeakViewerCount",
                table: "livestreams");

            migrationBuilder.DropColumn(
                name: "RecordingUrl",
                table: "livestreams");

            migrationBuilder.DropColumn(
                name: "TotalViews",
                table: "livestreams");

            migrationBuilder.DropColumn(
                name: "BankAccountSnapshot",
                table: "donations");

            migrationBuilder.DropColumn(
                name: "PaymentConfirmedAt",
                table: "donations");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "BehaviourLogs");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "BehaviourLogs");

            migrationBuilder.DropColumn(
                name: "Algorithm",
                table: "AiRecommendations");

            migrationBuilder.RenameColumn(
                name: "NetAmount",
                table: "settlements",
                newName: "Amount");

            migrationBuilder.RenameColumn(
                name: "GrossAmount",
                table: "payments",
                newName: "Amount");

            migrationBuilder.RenameColumn(
                name: "OnlineQuota",
                table: "LoungeShows",
                newName: "Quota");

            migrationBuilder.RenameColumn(
                name: "HlsUrl",
                table: "livestream_ticket_details",
                newName: "StreamUrl");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Ratings",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StreamKey",
                table: "livestream_ticket_details",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PerformerId",
                table: "Follows",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ticket_holds_PriceId_ExpiresAt",
                table: "ticket_holds",
                columns: new[] { "PriceId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_settlements_OwnerId_Status",
                table: "settlements",
                columns: new[] { "OwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_UserId_LoungeShowId",
                table: "Ratings",
                columns: new[] { "UserId", "LoungeShowId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Follows_PerformerId",
                table: "Follows",
                column: "PerformerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Follows_Performers_PerformerId",
                table: "Follows",
                column: "PerformerId",
                principalTable: "Performers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Ratings_Users_UserId",
                table: "Ratings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
