using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVenueTourAndSubscriptionTourScenes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxTourScenes",
                table: "subscription_packages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SubscriptionMaxTourScenesSnapshot",
                table: "payments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxTourScenesSnapshot",
                table: "owner_subscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "venue_tour_scenes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoungeId = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OrderIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venue_tour_scenes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_venue_tour_scenes_music_lounges_LoungeId",
                        column: x => x.LoungeId,
                        principalTable: "music_lounges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "venue_tour_hotspots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SceneId = table.Column<int>(type: "int", nullable: false),
                    TargetSceneId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Yaw = table.Column<double>(type: "float", nullable: false),
                    Pitch = table.Column<double>(type: "float", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InfoText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venue_tour_hotspots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_venue_tour_hotspots_venue_tour_scenes_SceneId",
                        column: x => x.SceneId,
                        principalTable: "venue_tour_scenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_venue_tour_hotspots_venue_tour_scenes_TargetSceneId",
                        column: x => x.TargetSceneId,
                        principalTable: "venue_tour_scenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_venue_tour_hotspots_SceneId",
                table: "venue_tour_hotspots",
                column: "SceneId");

            migrationBuilder.CreateIndex(
                name: "IX_venue_tour_hotspots_TargetSceneId",
                table: "venue_tour_hotspots",
                column: "TargetSceneId");

            migrationBuilder.CreateIndex(
                name: "IX_venue_tour_scenes_LoungeId",
                table: "venue_tour_scenes",
                column: "LoungeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "venue_tour_hotspots");

            migrationBuilder.DropTable(
                name: "venue_tour_scenes");

            migrationBuilder.DropColumn(
                name: "MaxTourScenes",
                table: "subscription_packages");

            migrationBuilder.DropColumn(
                name: "SubscriptionMaxTourScenesSnapshot",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "MaxTourScenesSnapshot",
                table: "owner_subscriptions");
        }
    }
}
