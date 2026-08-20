using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorFollowToLounge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Follows_Lounges_MusicLoungeId",
                table: "Follows");

            migrationBuilder.DropIndex(
                name: "IX_Follows_MusicLoungeId",
                table: "Follows");

            migrationBuilder.DropIndex(
                name: "IX_Follows_TargetType_TargetId",
                table: "Follows");

            migrationBuilder.DropIndex(
                name: "IX_Follows_UserId_TargetType_TargetId",
                table: "Follows");

            migrationBuilder.DropColumn(
                name: "MusicLoungeId",
                table: "Follows");

            migrationBuilder.DropColumn(
                name: "TargetType",
                table: "Follows");

            migrationBuilder.RenameColumn(
                name: "TargetId",
                table: "Follows",
                newName: "LoungeId");

            migrationBuilder.CreateIndex(
                name: "IX_Follows_LoungeId",
                table: "Follows",
                column: "LoungeId");

            migrationBuilder.CreateIndex(
                name: "IX_Follows_UserId_LoungeId",
                table: "Follows",
                columns: new[] { "UserId", "LoungeId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Follows_Lounges_LoungeId",
                table: "Follows",
                column: "LoungeId",
                principalTable: "Lounges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Follows_Lounges_LoungeId",
                table: "Follows");

            migrationBuilder.DropIndex(
                name: "IX_Follows_LoungeId",
                table: "Follows");

            migrationBuilder.DropIndex(
                name: "IX_Follows_UserId_LoungeId",
                table: "Follows");

            migrationBuilder.RenameColumn(
                name: "LoungeId",
                table: "Follows",
                newName: "TargetId");

            migrationBuilder.AddColumn<int>(
                name: "MusicLoungeId",
                table: "Follows",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetType",
                table: "Follows",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Follows_MusicLoungeId",
                table: "Follows",
                column: "MusicLoungeId");

            migrationBuilder.CreateIndex(
                name: "IX_Follows_TargetType_TargetId",
                table: "Follows",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Follows_UserId_TargetType_TargetId",
                table: "Follows",
                columns: new[] { "UserId", "TargetType", "TargetId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Follows_Lounges_MusicLoungeId",
                table: "Follows",
                column: "MusicLoungeId",
                principalTable: "Lounges",
                principalColumn: "Id");
        }
    }
}
