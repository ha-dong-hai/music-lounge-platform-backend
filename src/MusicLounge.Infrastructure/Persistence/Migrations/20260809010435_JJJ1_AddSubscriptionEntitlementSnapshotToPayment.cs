using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class JJJ1_AddSubscriptionEntitlementSnapshotToPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SubscriptionHasAiPosterSnapshot",
                table: "payments",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubscriptionMaxTicketsPerEventSnapshot",
                table: "payments",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubscriptionHasAiPosterSnapshot",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "SubscriptionMaxTicketsPerEventSnapshot",
                table: "payments");
        }
    }
}
