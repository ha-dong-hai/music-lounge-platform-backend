using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLACP391_LoungeStaffDeactivatedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeactivatedBy",
                table: "lounge_staff",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_lounge_staff_DeactivatedBy",
                table: "lounge_staff",
                column: "DeactivatedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_lounge_staff_users_DeactivatedBy",
                table: "lounge_staff",
                column: "DeactivatedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_lounge_staff_users_DeactivatedBy",
                table: "lounge_staff");

            migrationBuilder.DropIndex(
                name: "IX_lounge_staff_DeactivatedBy",
                table: "lounge_staff");

            migrationBuilder.DropColumn(
                name: "DeactivatedBy",
                table: "lounge_staff");
        }
    }
}
