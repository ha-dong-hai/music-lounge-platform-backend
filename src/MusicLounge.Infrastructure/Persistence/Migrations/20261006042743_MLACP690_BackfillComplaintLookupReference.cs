using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// MLACP-690. Bù mã tra cứu cho khiếu nại CŨ của người đã đăng nhập.
    ///
    /// Từ MLACP-690 mọi khiếu nại đều có mã (trước đây chỉ khách vãng lai), nên "Khiếu nại của tôi" hiện mã cho từng dòng —
    /// khiếu nại gửi trước đó sẽ trống nếu không bù. Chỉ ĐIỀN vào ô đang NULL, không sửa mã đã có, không xoá gì.
    ///
    /// Cùng dạng với mã do CreateComplaintCommandHandler cấp: 24 ký tự hex CHỮ HOA (Convert.ToHexString của 12 byte). Dùng
    /// NEWID() vì SQL Server tính nó RIÊNG cho từng dòng trong một UPDATE (khác RAND() chỉ tính một lần cho cả câu); 12 byte
    /// đầu của NEWID còn ~92 bit ngẫu nhiên — đủ để không đoán được. Chỉ mục duy nhất (lọc NOT NULL) trên cột vẫn chặn trùng:
    /// lỡ trùng thì cả migration lỗi và quay lại, không ghi dở.
    ///
    /// Down để trống có chủ ý: không phân biệt được mã bù với mã cấp lúc gửi; để lại mã không làm hỏng gì.
    /// </summary>
    public partial class MLACP690_BackfillComplaintLookupReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE complaints
                SET LookupReference = CONVERT(varchar(24), CAST(CONVERT(binary(16), NEWID()) AS binary(12)), 2)
                WHERE LookupReference IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
