namespace MusicLounge.Application.Catalog.DTOs;

/// <summary>
/// MLACP-625 — biểu phí và điều khoản tiền ĐANG ÁP DỤNG, công bố công khai (GET /catalog/money-terms).
///
/// Chủ dự án 04/10/2026: "các điều khoản liên quan tới tiền tôi muốn được đề cập rõ với khách hàng". Trước task này
/// web chỉ nói "trừ phí dịch vụ và thuế theo quy định" — không có con số — vì các tỉ lệ nằm trong system_config và
/// không endpoint công khai nào trả chúng ra. Gõ cứng số vào trang thì lần Admin đổi cấu hình kế tiếp trang sẽ nói sai
/// (cùng ngày, phần nghệ sĩ nhận từ tiền ủng hộ vừa đổi 88% → 86%). Nên mọi con số ở đây đọc từ đúng nguồn mà các
/// handler tiền đang đọc; client chỉ in ra.
///
/// Căn cứ công bố trước khi giao dịch: Luật Bảo vệ quyền lợi người tiêu dùng 2023 Điều 21 (cung cấp thông tin về giá,
/// phí, điều kiện giao dịch) và NĐ 52/2013 Điều 30, 31 (sàn công bố chính sách, giá và các khoản phí).
///
/// CÓ GHI NGÀY CẬP NHẬT VÀ NHẬT KÝ THAY ĐỔI (chủ dự án 04/10/2026: "sau này có ghi nhận mới cũng có cơ chế cập nhật và
/// phải cho người khác biết đã được cập nhật lúc nào để có cơ sở tham chiếu"). Không dựng cơ chế mới: mọi lần Admin đổi
/// một tham số đã sinh một dòng SystemConfigHistory bất biến (D9); endpoint này chỉ CÔNG BỐ phần lịch sử của các khoá
/// tiền. Nhờ vậy không ai phải nhớ "sửa ngày cập nhật" — đổi cấu hình là ngày và nhật ký tự đổi theo. Cách làm tham
/// chiếu: Eventbrite ghi ngày "Updated" đầu điều khoản và tách ngày hiệu lực; Stripe mỗi lần đổi phí ra một thông
/// báo ghi rõ mức cũ, mức mới và ngày áp dụng. Lý do Admin ghi khi đổi (Note) KHÔNG công bố — đó là ghi chú nội bộ.
///
/// GIỚI HẠN CỐ Ý: thay đổi có hiệu lực NGAY lúc Admin lưu (EffectiveFrom = lúc đổi); chưa có "báo trước N ngày rồi
/// mới áp dụng". Đường nâng cấp khi cần: cho PUT /admin/system-config nhận ngày hiệu lực tương lai và để các handler
/// tiền đọc giá trị theo ngày — một thay đổi lớn, cần quyết định riêng.
///
/// Chỉ chứa thứ ĐỌC ĐƯỢC TỪ CẤU HÌNH hoặc từ hằng có tên lớp. Không có trường nào kiểu "luôn đúng" gõ tay ngoài
/// <see cref="DonationMoneyTermsDto.Refundable"/> — trường đó lấy từ PublicDonationStatement, nơi đã giải thích vì sao.
/// </summary>
/// <param name="UpdatedAt">Lần gần nhất một điều khoản tiền được đổi; null = chưa đổi lần nào kể từ khi hệ thống chạy.</param>
/// <param name="Changes">Nhật ký thay đổi, mới nhất lên đầu.</param>
public sealed record MoneyTermsDto(
    TicketMoneyTermsDto Ticket,
    RefundMoneyTermsDto Refund,
    DonationMoneyTermsDto Donation,
    SettlementMoneyTermsDto Settlement,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<MoneyTermChangeDto> Changes);

/// <param name="Key">Khoá cấu hình đã đổi (ví dụ donation_performer_share_rate) — client tự dịch ra nhãn.</param>
/// <param name="OldValue">Giá trị trước khi đổi.</param>
/// <param name="NewValue">Giá trị sau khi đổi.</param>
/// <param name="EffectiveFrom">Thời điểm giá trị mới bắt đầu áp dụng cho giao dịch mới.</param>
public sealed record MoneyTermChangeDto(string Key, string? OldValue, string NewValue, DateTimeOffset EffectiveFrom);

/// <param name="PlatformCommissionRate">Phí nền tảng trên tiền vé mua trực tuyến — trừ vào phần phòng trà nhận.</param>
/// <param name="VatRate">Thuế GTGT khấu trừ tại nguồn với phòng trà là hộ/cá nhân kinh doanh.</param>
/// <param name="PersonalIncomeTaxRate">Thuế TNCN khấu trừ tại nguồn với phòng trà là hộ/cá nhân kinh doanh.</param>
/// <param name="HoldMinutes">Số phút giữ chỗ để thanh toán.</param>
/// <param name="HoldMaxQuantity">Số vé tối đa một lần giữ chỗ.</param>
/// <param name="WalkInCashGoesThroughPlatform">Vé bán tại quầy bằng tiền mặt có đi qua nền tảng (và chịu phí) hay không.</param>
public sealed record TicketMoneyTermsDto(
    decimal PlatformCommissionRate,
    decimal VatRate,
    decimal PersonalIncomeTaxRate,
    int HoldMinutes,
    int HoldMaxQuantity,
    bool WalkInCashGoesThroughPlatform);

/// <param name="ReviewHours">Hạn quản trị viên xử lý một yêu cầu hoàn tiền.</param>
/// <param name="AutoApproveAfterHours">Quá số giờ này kể từ lúc gửi mà chưa ai xử lý thì hệ thống tự duyệt.</param>
/// <param name="GatewayRefundWindowDays">Số ngày kể từ lúc thanh toán mà tiền còn hoàn được về đúng phương thức đã trả;
/// quá hạn thì hoàn bằng chuyển khoản tới tài khoản người mua khai.</param>
public sealed record RefundMoneyTermsDto(
    int ReviewHours,
    int AutoApproveAfterHours,
    int GatewayRefundWindowDays);

/// <param name="PerformerShareRate">Phần nghệ sĩ nhận trên mỗi khoản ủng hộ.</param>
/// <param name="VenueShareRate">Phần phòng trà giữ lại = phần còn lại sau nghệ sĩ, phí và thuế (với phòng trà là hộ/cá
/// nhân kinh doanh; doanh nghiệp đã xác minh không bị khấu trừ thuế nên phần này lớn hơn).</param>
/// <param name="VenuePayoutDays">Số ngày phòng trà có để chuyển tiền cho nghệ sĩ, tính từ lúc phòng trà nhận tiền.</param>
/// <param name="VenueWarningDays">Quá số ngày này mà chưa chuyển thì phòng trà bị cảnh cáo.</param>
/// <param name="MaxAmount">Số tiền tối đa của một khoản ủng hộ (đồng).</param>
public sealed record DonationMoneyTermsDto(
    decimal PerformerShareRate,
    decimal PlatformCommissionRate,
    decimal VatRate,
    decimal PersonalIncomeTaxRate,
    decimal VenueShareRate,
    int VenuePayoutDays,
    int VenueWarningDays,
    bool Refundable,
    decimal MaxAmount);

/// <param name="FirstTrancheHoursAfterShow">Đợt 1 chi sau khi buổi diễn kết thúc chừng này giờ.</param>
/// <param name="FinalTrancheDaysAfterShow">Đợt cuối chi sau khi buổi diễn kết thúc chừng này ngày.</param>
/// <param name="NewVenueFirstTrancheRate">Tỉ lệ đợt 1 của phòng trà mới.</param>
/// <param name="StandardFirstTrancheRate">Tỉ lệ đợt 1 khi điểm đánh giá đạt <paramref name="StandardMinScore"/>.</param>
/// <param name="PremiumFirstTrancheRate">Tỉ lệ đợt 1 khi điểm đạt <paramref name="PremiumMinScore"/> và đã tổ chức
/// xong ít nhất <paramref name="PremiumMinShows"/> buổi.</param>
public sealed record SettlementMoneyTermsDto(
    int FirstTrancheHoursAfterShow,
    int FinalTrancheDaysAfterShow,
    decimal NewVenueFirstTrancheRate,
    decimal StandardFirstTrancheRate,
    decimal PremiumFirstTrancheRate,
    decimal StandardMinScore,
    decimal PremiumMinScore,
    int PremiumMinShows);
