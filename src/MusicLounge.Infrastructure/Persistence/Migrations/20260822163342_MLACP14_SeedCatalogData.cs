using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLACP14_SeedCatalogData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "event_categories",
                columns: new[] { "Id", "Description", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, null, true, "Đêm nhạc thường" },
                    { 2, null, true, "Mini Show" },
                    { 3, null, true, "Sự kiện riêng" },
                    { 4, null, true, "Họp báo" }
                });

            migrationBuilder.InsertData(
                table: "moods",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Hoài niệm" },
                    { 2, "Tiền chiến" },
                    { 3, "Lãng mạn" },
                    { 4, "Chill" },
                    { 5, "Sôi động" },
                    { 6, "Nhẹ nhàng" }
                });

            migrationBuilder.InsertData(
                table: "music_genres",
                columns: new[] { "Id", "Name", "NameEn" },
                values: new object[,]
                {
                    { 1, "Jazz", "Jazz" },
                    { 2, "Acoustic", "Acoustic" },
                    { 3, "Ballad", "Ballad" },
                    { 4, "Bolero", "Bolero" },
                    { 5, "Pop", "Pop" },
                    { 6, "Trữ tình", null },
                    { 7, "R&B", "R&B" },
                    { 8, "Cổ điển", "Classical" }
                });

            migrationBuilder.InsertData(
                table: "venue_atmospheres",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Ấm cúng" },
                    { 2, "Sang trọng" },
                    { 3, "Mộc mạc" },
                    { 4, "Nghệ thuật" },
                    { 5, "Hiện đại" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "event_categories",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "event_categories",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "event_categories",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "event_categories",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "moods",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "moods",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "moods",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "moods",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "moods",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "moods",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "music_genres",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "music_genres",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "music_genres",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "music_genres",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "music_genres",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "music_genres",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "music_genres",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "music_genres",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "venue_atmospheres",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "venue_atmospheres",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "venue_atmospheres",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "venue_atmospheres",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "venue_atmospheres",
                keyColumn: "Id",
                keyValue: 5);
        }
    }
}
