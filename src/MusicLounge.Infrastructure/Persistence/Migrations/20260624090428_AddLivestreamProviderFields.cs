using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLivestreamProviderFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CloudflareInputId",
                table: "livestreams",
                newName: "ProviderRef");

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "livestreams",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Provider",
                table: "livestreams");

            migrationBuilder.RenameColumn(
                name: "ProviderRef",
                table: "livestreams",
                newName: "CloudflareInputId");
        }
    }
}
