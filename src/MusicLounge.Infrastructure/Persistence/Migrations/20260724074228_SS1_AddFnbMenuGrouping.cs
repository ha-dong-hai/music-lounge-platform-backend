using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SS1_AddFnbMenuGrouping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_fnb_menu_items_music_lounges_LoungeId",
                table: "fnb_menu_items");

            migrationBuilder.RenameColumn(
                name: "LoungeId",
                table: "fnb_menu_items",
                newName: "MenuId");

            migrationBuilder.RenameIndex(
                name: "IX_fnb_menu_items_LoungeId",
                table: "fnb_menu_items",
                newName: "IX_fnb_menu_items_MenuId");

            migrationBuilder.CreateTable(
                name: "fnb_menus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoungeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fnb_menus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fnb_menus_music_lounges_LoungeId",
                        column: x => x.LoungeId,
                        principalTable: "music_lounges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fnb_menus_LoungeId",
                table: "fnb_menus",
                column: "LoungeId");

            migrationBuilder.AddForeignKey(
                name: "FK_fnb_menu_items_fnb_menus_MenuId",
                table: "fnb_menu_items",
                column: "MenuId",
                principalTable: "fnb_menus",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_fnb_menu_items_fnb_menus_MenuId",
                table: "fnb_menu_items");

            migrationBuilder.DropTable(
                name: "fnb_menus");

            migrationBuilder.RenameColumn(
                name: "MenuId",
                table: "fnb_menu_items",
                newName: "LoungeId");

            migrationBuilder.RenameIndex(
                name: "IX_fnb_menu_items_MenuId",
                table: "fnb_menu_items",
                newName: "IX_fnb_menu_items_LoungeId");

            migrationBuilder.AddForeignKey(
                name: "FK_fnb_menu_items_music_lounges_LoungeId",
                table: "fnb_menu_items",
                column: "LoungeId",
                principalTable: "music_lounges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
