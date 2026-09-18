using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// MLACP-455. Đổi tên 3 giá trị <c>notifications.ReferenceType</c> cho khớp từ vựng đã thống nhất
    /// (<c>NotificationReferenceTypes</c>): <c>fnbOrder</c> → <c>fnb_order</c>, <c>kyc-review</c> → <c>kyc_review</c>,
    /// <c>refund</c> → <c>refund_request</c>.
    ///
    /// Không có thay đổi nào về cấu trúc bảng — viết tay vì model không đổi nên scaffold sinh ra migration rỗng.
    /// Cần chạy: thông báo cũ trong database vẫn mang tên cũ, nếu không đổi thì lịch sử thông báo của người dùng
    /// trỏ sai và deep-link gãy. Mỗi lệnh là idempotent (chạy lại không đổi thêm dòng nào).
    ///
    /// Thứ tự triển khai: <b>deploy code mới TRƯỚC, áp migration SAU</b> — code cũ còn ghi tên cũ, nếu áp trước thì
    /// giữa hai bước lại sinh ra dòng mang tên cũ.
    /// </summary>
    public partial class MLACP455_CanonicalNotificationReferenceTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE dbo.notifications SET ReferenceType = N'fnb_order' WHERE ReferenceType = N'fnbOrder';");
            migrationBuilder.Sql("UPDATE dbo.notifications SET ReferenceType = N'kyc_review' WHERE ReferenceType = N'kyc-review';");
            migrationBuilder.Sql("UPDATE dbo.notifications SET ReferenceType = N'refund_request' WHERE ReferenceType = N'refund';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Hai cái đầu đảo ngược được chính xác vì tên cũ chỉ thuộc về chúng.
            migrationBuilder.Sql("UPDATE dbo.notifications SET ReferenceType = N'fnbOrder' WHERE ReferenceType = N'fnb_order';");
            migrationBuilder.Sql("UPDATE dbo.notifications SET ReferenceType = N'kyc-review' WHERE ReferenceType = N'kyc_review';");

            // refund_request thì KHÔNG đảo được: trước khi chạy Up đã có sẵn dòng mang tên refund_request, nên không còn
            // cách nào phân biệt dòng nào vốn là 'refund'. Cố đảo sẽ đổi nhầm cả dòng vốn đúng. Cần khôi phục thật thì
            // phục hồi database theo thời điểm (Azure SQL giữ 7 ngày).
        }
    }
}
