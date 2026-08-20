using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ZZ1_UniqueActiveStaffPerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lounge_staff_UserId",
                table: "lounge_staff");

            migrationBuilder.CreateIndex(
                name: "IX_lounge_staff_UserId",
                table: "lounge_staff",
                column: "UserId",
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lounge_staff_UserId",
                table: "lounge_staff");

            migrationBuilder.CreateIndex(
                name: "IX_lounge_staff_UserId",
                table: "lounge_staff",
                column: "UserId");
        }
    }
}
