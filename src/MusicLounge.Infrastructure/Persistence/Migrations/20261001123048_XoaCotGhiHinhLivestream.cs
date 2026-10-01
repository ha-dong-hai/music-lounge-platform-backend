using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// MLACP-511 — bước CONTRACT của việc bỏ xem lại livestream (chủ dự án chốt "bỏ hẳn" 01/10/2026).
    /// <para><b>THỨ TỰ BẮT BUỘC:</b> chỉ áp SAU KHI code MLACP-510 đã chạy trên Azure. Code trước MLACP-510 còn đọc/ghi hai
    /// cột này — xoá cột trước thì bản đang chạy lỗi ngay khi EF đọc bảng livestreams.</para>
    /// <para><b>DỮ LIỆU MẤT:</b> đường dẫn bản ghi Mux và hạn xem lại của các buổi đã phát. Không còn tính năng nào đọc chúng.
    /// Trước khi áp: diễn tập trên bản sao (az sql db copy), ghi số dòng có RecordingUrl khác null vào deploy_log.md; Azure SQL
    /// giữ bản khôi phục theo thời điểm 7 ngày.</para>
    /// </summary>
    public partial class XoaCotGhiHinhLivestream : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecordingUrl",
                table: "livestreams");

            migrationBuilder.DropColumn(
                name: "ReplayAvailableUntil",
                table: "livestreams");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecordingUrl",
                table: "livestreams",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReplayAvailableUntil",
                table: "livestreams",
                type: "datetimeoffset",
                nullable: true);
        }
    }
}
