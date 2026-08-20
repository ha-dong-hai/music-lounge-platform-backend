using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EEE1_OwnerSubscriptionActiveUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_owner_subscriptions_OwnerId",
                table: "owner_subscriptions",
                column: "OwnerId",
                unique: true,
                filter: "[Status] = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_owner_subscriptions_OwnerId",
                table: "owner_subscriptions");
        }
    }
}
