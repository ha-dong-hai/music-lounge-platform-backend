using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// BR-01 (MLACP-307). Ba cột lưu vết quyết định duyệt hồ sơ phòng trà, cộng một bước chuyển
    /// tiếp dữ liệu bắt buộc.
    ///
    /// Trước migration này, Pending là trạng thái mặc định nhưng không chặn gì cả, nên mọi phòng
    /// trà trên hệ thống — kể cả những phòng đang bán vé thật — đều đang nằm ở Pending. Bật cổng
    /// kiểm duyệt lên mà không xử lý dữ liệu cũ thì tất cả chúng biến mất khỏi danh sách công khai
    /// và không nộp duyệt được buổi diễn nào nữa, ngay giây phút deploy.
    ///
    /// Cách xử lý: chỉ chuyển sang Approved những phòng trà đã thực sự hoạt động — tức là đã có ít
    /// nhất một buổi diễn ra khỏi trạng thái Draft. Đó là bằng chứng khách quan rằng chỗ đó đã vận
    /// hành dưới luật cũ, và giữ nguyên nó là giữ đúng hiện trạng.
    ///
    /// Những hồ sơ còn lại — tạo ra rồi bỏ đó, chưa có buổi diễn nào — vẫn ở Pending, vì đó đúng
    /// nghĩa là hồ sơ đang chờ duyệt. Chúng sẽ hiện trong hàng đợi GET /admin/venues/pending để
    /// Admin xét thật. Duyệt hàng loạt cả những hồ sơ chưa ai nhìn qua thì cổng kiểm duyệt này
    /// mất ý nghĩa ngay từ ngày đầu.
    ///
    /// StatusReviewedBy và StatusReviewedAt để trống ở bước này: không có ai duyệt cả, và ghi một
    /// mốc thời gian xét duyệt vào đó là dựng lên một quyết định chưa từng xảy ra.
    /// </summary>
    public partial class MLACP307_VenueApprovalGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StatusReviewNote",
                table: "music_lounges",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StatusReviewedAt",
                table: "music_lounges",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusReviewedBy",
                table: "music_lounges",
                type: "int",
                nullable: true);

            // Cả hai cột Status đều lưu dạng chuỗi (HasConversion<string>), nên so sánh bằng tên
            // enum chứ không phải số thứ tự.
            migrationBuilder.Sql("""
                UPDATE music_lounges
                SET Status = 'Approved',
                    StatusReviewNote = N'Tự động chuyển sang Approved khi bật cổng kiểm duyệt phòng trà (BR-01, MLACP-307): phòng trà này đã có buổi diễn hoạt động từ trước thời điểm đó.'
                WHERE Status = 'Pending'
                  AND EXISTS (
                      SELECT 1 FROM lounge_shows s
                      WHERE s.LoungeId = music_lounges.Id AND s.Status <> 'Draft');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Không đảo ngược bước chuyển dữ liệu ở trên: sau khi migration này chạy, không còn
            // phân biệt được phòng trà nào được Approved do chuyển tiếp và phòng nào do Admin thật
            // sự bấm duyệt. Đẩy tất cả về Pending sẽ khoá luôn cả những phòng đã được duyệt hợp lệ.
            migrationBuilder.DropColumn(
                name: "StatusReviewNote",
                table: "music_lounges");

            migrationBuilder.DropColumn(
                name: "StatusReviewedAt",
                table: "music_lounges");

            migrationBuilder.DropColumn(
                name: "StatusReviewedBy",
                table: "music_lounges");
        }
    }
}
