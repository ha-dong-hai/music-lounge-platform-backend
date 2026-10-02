using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// MLACP-526. Bù <c>payments.PayerId</c> cho thanh toán vé online CŨ.
    ///
    /// PurchaseTicket trước đây không ghi người trả tiền, nên chốt chặn MLACP-370 ở CancelTicket (đọc PayerId) không kích
    /// hoạt với bất kỳ vé online nào đã bán: người nhận chuyển nhượng tự huỷ được vé do người khác trả tiền. Code đã sửa
    /// cho vé bán từ nay; migration này vá phần đã bán (E2E 02/10 trên bản sao dữ liệu: 44/44 dòng NULL).
    ///
    /// Không suy được từ <c>ticket_holds</c> (giữ chỗ bị dọn sau khi dùng — bảng trống). Hai nguồn còn lại, theo thứ tự:
    /// 1. Vé ĐÃ chuyển nhượng: AcceptTicketTransfer gửi thông báo "Chuyển nhượng vé thành công" cho người chuyển, gắn
    ///    ReferenceId = mã vé. Người nhận thông báo SỚM NHẤT là người mua gốc (chuyển nhiều chặng thì chặng đầu do người
    ///    mua gốc chuyển). Người dùng không có endpoint xoá thông báo nên dấu vết này không bị mất.
    /// 2. Vé CHƯA từng chuyển: người đang giữ (tickets.BuyerId) chính là người mua — chỉ dùng khi MỌI vé của thanh toán đó
    ///    cùng một người giữ.
    /// Trường hợp còn lại (mơ hồ) để NULL, không đoán: CancelTicket coi NULL là "không chặn" như trước — không tệ hơn hiện
    /// trạng, còn đoán sai thì chặn nhầm chính người mua.
    ///
    /// Thông báo gửi trước MLACP-489 không có TitleEn, nên khớp theo cả tiêu đề tiếng Việt lẫn tiếng Anh. So mã vé theo
    /// kiểu GUID (TRY_CAST) chứ không theo chuỗi: ReferenceId lưu chữ thường, CAST(guid) ra chữ hoa — so chuỗi chỉ đúng
    /// nhờ collation không phân biệt hoa thường, không nên dựa vào đó.
    /// </summary>
    public partial class MLACP526_BackfillTicketPayerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE p
                SET p.PayerId = COALESCE(
                    (SELECT TOP 1 n.UserId
                     FROM tickets t
                     INNER JOIN notifications n
                        ON n.ReferenceType = 'ticket'
                       AND TRY_CAST(n.ReferenceId AS uniqueidentifier) = t.Id
                       AND (n.Title = N'Chuyển nhượng vé thành công' OR n.TitleEn = 'Ticket transfer completed')
                     WHERE t.PaymentId = p.Id
                     ORDER BY n.CreatedAt, n.Id),
                    (SELECT MAX(t.BuyerId)
                     FROM tickets t
                     WHERE t.PaymentId = p.Id
                     HAVING COUNT(DISTINCT t.BuyerId) = 1 AND COUNT(t.BuyerId) = COUNT(*)))
                FROM payments p
                WHERE p.ReferenceType = 'TicketHold'
                  AND p.PayerId IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Không đảo: sau khi áp, không còn phân biệt được dòng do migration này điền với dòng do code mới ghi lúc mua
            // (cả hai đều đúng). Gỡ bản phát hành thì dữ liệu điền đúng vẫn nên giữ.
        }
    }
}
