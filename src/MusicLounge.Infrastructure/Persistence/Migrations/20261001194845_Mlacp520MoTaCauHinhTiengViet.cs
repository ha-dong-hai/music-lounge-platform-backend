using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Mlacp520MoTaCauHinhTiengViet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 4,
                column: "Description",
                value: "Số giờ sau khi buổi diễn kết thúc thì chi trả đợt đầu cho phòng trà (tỷ lệ đợt đầu tuỳ hạng phòng trà)");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 5,
                column: "Description",
                value: "Số ngày sau khi buổi diễn kết thúc thì chi trả phần còn lại cho phòng trà");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 6,
                column: "Description",
                value: "Tỷ lệ tối thiểu giữa thời lượng diễn thực tế và theo lịch để tự động chi trả phần còn lại; thấp hơn thì khoản này chờ Admin xem xét (D16)");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 7,
                column: "Description",
                value: "Hạng Mới: tỷ lệ chi trả đợt đầu cho phòng trà có điểm uy tín dưới 3,5 hoặc dưới 3 buổi diễn (D3)");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 8,
                column: "Description",
                value: "Hạng Chuẩn: tỷ lệ chi trả đợt đầu cho phòng trà có điểm uy tín từ 3,5 đến 4,2 (D3)");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 9,
                column: "Description",
                value: "Hạng Premium: tỷ lệ chi trả đợt đầu cho phòng trà có điểm uy tín từ 4,2 trở lên VÀ từ 10 buổi diễn trở lên (D3)");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 10,
                column: "Description",
                value: "Điểm uy tín tối thiểu để phòng trà đạt Hạng Chuẩn (D3)");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 11,
                column: "Description",
                value: "Điểm uy tín tối thiểu để phòng trà đạt Hạng Premium (D3)");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 12,
                column: "Description",
                value: "Số buổi diễn đã hoàn tất tối thiểu để phòng trà đạt Hạng Premium (D3)");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 15,
                column: "Description",
                value: "Thời hạn (giờ) để Admin xử lý nội dung bị gắn cờ — NĐ 147/2024");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 16,
                column: "Description",
                value: "Số phút giữ chỗ trong lúc khách thanh toán; quá hạn thì nhả chỗ cho người khác (§6.3)");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 17,
                column: "Description",
                value: "Số ngày, tính từ lúc phòng trà nhận tiền donate, để phòng trà chuyển cho nghệ sĩ; quá hạn mà chủ phòng trà không phản hồi thì hệ thống tự xác nhận (D4)");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 18,
                column: "Description",
                value: "Số ngày sau khi buổi diễn kết thúc mà khán giả còn được đánh giá (§6.13)");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 19,
                column: "Description",
                value: "Số giờ để Admin xem xét đơn kháng cáo án phạt (§6.17)");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 20,
                column: "Description",
                value: "Tự động chấp nhận đơn kháng cáo khi Admin xử lý quá hạn (§6.17)");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 21,
                column: "Description",
                value: "Tỷ lệ trên tổng tiền donate được chuyển cho nghệ sĩ (§6.5 chặng 2)");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 22,
                column: "Description",
                value: "Thời hạn mục tiêu (giờ) để giải quyết khiếu nại của người tiêu dùng — mục tiêu vận hành của nền tảng; NĐ 85/2021 yêu cầu nền tảng làm đầu mối nhưng không quy định số giờ");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 23,
                column: "Description",
                value: "Mã phiên bản Điều khoản sử dụng/Chính sách quyền riêng tư đang công bố; người đăng ký mới đồng ý theo phiên bản này — căn cứ đồng ý theo Luật 91/2025/QH15");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 24,
                column: "Description",
                value: "Số ngày làm việc tối thiểu từ lúc đăng hoặc dời lịch tới ngày diễn — NĐ 144/2020 Điều 10");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 25,
                column: "Description",
                value: "Số giờ báo trước khi án tạm đình chỉ có hiệu lực (§6.8)");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 26,
                column: "Description",
                value: "Số ngày báo trước khi án cấm hoạt động có hiệu lực (§6.8)");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 27,
                column: "Description",
                value: "Số vé tối đa cho mỗi lần giữ chỗ mua online — chặn đầu cơ vé");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 28,
                column: "Description",
                value: "Số vé tối đa cho mỗi lần bán tại quầy — chặn lạm dụng");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 29,
                column: "Description",
                value: "Số tiền tối đa cho một lần donate (VNĐ) — chặn gian lận");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 30,
                column: "Description",
                value: "Số giờ trước khi yêu cầu chuyển nhượng vé không được trả lời tự động bị huỷ");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 31,
                column: "Description",
                value: "Số lần tạo poster AI tối đa cho mỗi buổi diễn (tính cả lần lỗi)");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 33,
                column: "Description",
                value: "Hạn vào cửa: buổi diễn phải còn ít nhất số phút này thì mới bán vé (BR-31, giống mặc định vé không chọn chỗ của Eventbrite)");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 34,
                column: "Description",
                value: "Khoảng cách tối thiểu (phút) giữa hai buổi diễn liên tiếp ở cùng phòng trà — thời gian tiễn khán giả trước và đón khán giả sau. 30 là mức thấp của khoảng 30–60 phút phổ biến ở các địa điểm hòa nhạc (CF1)");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 35,
                column: "Description",
                value: "Danh sách JSON các từ/cụm từ khiến lời nhắn donate không hiện trên thông báo livestream (so khớp nguyên từ, không phân biệt hoa thường và dấu). Khoản donate vẫn được thông báo (MLACP-360)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 4,
                column: "Description",
                value: "Hours after show end to release the partial (Tier pre_rate) tranche");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 5,
                column: "Description",
                value: "Days after show end to release the final tranche");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 6,
                column: "Description",
                value: "D16: min actual/scheduled ratio for auto-release final settlement");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 7,
                column: "Description",
                value: "D3 Tier Mới: pre_rate for venues score<3.5 or <3 shows");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 8,
                column: "Description",
                value: "D3 Tier Chuẩn: pre_rate for venues score 3.5–4.2");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 9,
                column: "Description",
                value: "D3 Tier Premium: pre_rate for venues score≥4.2 AND ≥10 shows");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 10,
                column: "Description",
                value: "D3: reputation_score threshold to qualify for Tier Chuẩn");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 11,
                column: "Description",
                value: "D3: reputation_score threshold to qualify for Tier Premium");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 12,
                column: "Description",
                value: "D3: minimum completed shows to qualify for Tier Premium");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 15,
                column: "Description",
                value: "Admin SLA to review flagged content — NĐ 147/2024");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 16,
                column: "Description",
                value: "Checkout hold duration before slot released — §6.3");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 17,
                column: "Description",
                value: "Days before auto-confirm donation if Owner inactive — D4");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 18,
                column: "Description",
                value: "Days after show end to submit rating — §6.13");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 19,
                column: "Description",
                value: "Hours for Admin to review penalty appeal — §6.17");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 20,
                column: "Description",
                value: "Auto-approve appeal when Admin misses SLA — §6.17");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 21,
                column: "Description",
                value: "§6.5 chặng 2: % of gross donation forwarded to performer");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 22,
                column: "Description",
                value: "Operational target to resolve a consumer complaint — NĐ 85/2021");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 23,
                column: "Description",
                value: "Version label of the currently-published ToS/Privacy Policy — Luật 91/2025/QH15 lawful-basis consent");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 24,
                column: "Description",
                value: "Min business days between publish/reschedule and show date — NĐ 144/2020 Điều 10");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 25,
                column: "Description",
                value: "Notice hours before a Suspension penalty takes effect — §6.8");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 26,
                column: "Description",
                value: "Notice days before a Ban penalty takes effect — §6.8");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 27,
                column: "Description",
                value: "Max tickets per checkout hold — anti-scalping ceiling");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 28,
                column: "Description",
                value: "Max tickets per walk-in/box-office sale — anti-abuse ceiling");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 29,
                column: "Description",
                value: "Max single donation amount (VND) — anti-fraud ceiling");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 30,
                column: "Description",
                value: "Hours before an unanswered ticket-transfer request auto-cancels");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 31,
                column: "Description",
                value: "Max AI poster generation attempts (incl. failures) per show");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 33,
                column: "Description",
                value: "Last-entry cutoff: minutes of the show that must still remain for a ticket to be sold — BR-31, matches Eventbrite's general-admission default");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 34,
                column: "Description",
                value: "Minimum gap between two consecutive shows at one venue — time to clear one audience and admit the next. 30 is the low end of the 30-60 min industry range for standard concert venues (CF1)");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 35,
                column: "Description",
                value: "JSON array of words/phrases that keep a donation message off the livestream alert (whole-word, case- and diacritic-insensitive). The donation itself is still announced (MLACP-360)");
        }
    }
}
