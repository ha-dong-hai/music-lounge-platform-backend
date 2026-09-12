using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLACP378_DropDeadPaymentSettlementStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SettlementStatus",
                table: "payments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SettlementStatus",
                table: "payments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NotApplicable");
        }
    }
}
