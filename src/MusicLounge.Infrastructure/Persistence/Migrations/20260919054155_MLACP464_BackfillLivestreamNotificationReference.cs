using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// MLACP-464. Sửa các thông báo CŨ để chúng trỏ đúng chỗ.
    ///
    /// MLACP-460 đã đổi thông báo kết quả duyệt buổi phát trực tiếp sang mang mã buổi hòa nhạc (mọi đường dẫn của
    /// frontend đều nhận mã đó). Nhưng thông báo gửi TRƯỚC thay đổi ấy vẫn mang mã buổi phát — và từ phía frontend
    /// chúng giống hệt thông báo mới: cùng <c>referenceType = 'livestream'</c>, cùng là một con số.
    ///
    /// Hệ quả nếu không sửa: frontend gắn link cho loại này thì một phần người dùng bấm vào sẽ mở NHẦM buổi hòa nhạc
    /// khác (hai bảng đánh số riêng nên mã dễ trùng số), và không ai biết vì trang vẫn mở ra bình thường. Frontend đã
    /// cố ý chưa gắn link, chờ đúng bản vá này.
    ///
    /// Chạy một lần, TRƯỚC khi bản có MLACP-460 lên production — lúc đó mọi dòng mang loại này đều là dòng cũ, không cần
    /// mốc thời gian để phân biệt. EF ghi lại migration đã áp nên không chạy lại lần hai.
    /// </summary>
    public partial class MLACP464_BackfillLivestreamNotificationReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TRY_CONVERT: dòng nào có ReferenceId không phải số nguyên thì bỏ qua thay vì làm hỏng cả câu lệnh.
            // Chỉ đụng đúng dòng khớp một buổi phát có thật.
            migrationBuilder.Sql("""
                UPDATE n
                SET n.ReferenceId = CAST(l.LoungeShowId AS nvarchar(100))
                FROM notifications n
                INNER JOIN livestreams l ON TRY_CONVERT(int, n.ReferenceId) = l.Id
                WHERE n.ReferenceType = 'livestream';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Đảo được vì mỗi buổi hòa nhạc có nhiều nhất MỘT buổi phát (chỉ mục duy nhất trên livestreams.LoungeShowId),
            // nên từ mã buổi hòa nhạc suy ngược ra mã buổi phát là xác định.
            migrationBuilder.Sql("""
                UPDATE n
                SET n.ReferenceId = CAST(l.Id AS nvarchar(100))
                FROM notifications n
                INNER JOIN livestreams l ON TRY_CONVERT(int, n.ReferenceId) = l.LoungeShowId
                WHERE n.ReferenceType = 'livestream';
                """);
        }
    }
}
