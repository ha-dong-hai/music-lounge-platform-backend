using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLACP283_CorrectTaxConfigLegalAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 1,
                column: "Description",
                value: "VNPay gateway processing fee (2%) — mức thương mại của cổng, không do văn bản pháp luật ấn định");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 2,
                column: "Description",
                value: "Hoa hồng nền tảng (5%) — quyết định thương mại của dự án, KHÔNG do nghị định nào quy định");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 3,
                column: "Description",
                value: "Thuế GTGT khấu trừ tại nguồn (5% — tỷ lệ cho DỊCH VỤ theo NĐ 117/2025/NĐ-CP). Chưa khấu trừ TNCN.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 1,
                column: "Description",
                value: "VNPay gateway processing fee (2%) — NĐ 52/2024");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 2,
                column: "Description",
                value: "Platform fee rate (5%) — NĐ 117/2025");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 3,
                column: "Description",
                value: "VAT withheld at source (5%) — NĐ 117/2025");
        }
    }
}
