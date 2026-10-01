using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class SystemConfigConfiguration : IEntityTypeConfiguration<SystemConfig>
{
    public void Configure(EntityTypeBuilder<SystemConfig> b)
    {
        b.ToTable("system_config");
        b.HasKey(x => x.Id);
        b.Property(x => x.ConfigKey).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.ConfigKey).IsUnique();
        b.Property(x => x.ConfigValue).HasMaxLength(500).IsRequired();
        b.Property(x => x.DataType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Description).HasMaxLength(500);

        b.HasOne(x => x.UpdatedByUser)
            .WithMany()
            .HasForeignKey(x => x.UpdatedBy)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasMany(x => x.History)
            .WithOne(h => h.Config)
            .HasForeignKey(h => h.ConfigKey)
            .HasPrincipalKey(x => x.ConfigKey)
            .OnDelete(DeleteBehavior.Cascade);

        var seed = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        b.HasData(
            // Payment & Tax.
            //
            // Careful with what is and is not a legal citation here. NĐ 117/2025/NĐ-CP (09/6/2025,
            // hiệu lực 01/7/2025) governs tax management for households/individuals selling through
            // e-commerce and digital platforms: a platform WITH a payment function must withhold and
            // remit on their behalf, at the moment the transaction is confirmed and paid. Its
            // percentage for SERVICES is 5% VAT — which is where tax_rate's 5% legitimately comes
            // from, this platform selling event access being a service.
            //
            // platform_commission_rate is NOT that. No decree sets a platform's own commercial
            // commission; the 5% is this project's business decision and was previously described as
            // if NĐ 117/2025 mandated it. Corrected, because a legal citation nobody can produce on
            // request is worse than none.
            //
            // MLACP-289 closed the gap this comment used to describe. NĐ 117/2025 has a
            // payment-handling platform withhold BOTH VAT and personal income tax, and only for
            // hộ/cá nhân kinh doanh — a doanh nghiệp declares its own. User.BusinessType now tells
            // the two apart (and TaxWithholdingPolicy is what reads it), and personal income tax
            // has its own rate below and its own ledger account.
            //
            // That rate is seeded at 0, not at the decree's 2%. Turning it on takes money out of
            // every household seller's share, so it is a decision an Admin makes deliberately
            // through PUT /admin/system-config — where the reason is recorded and the old value
            // kept — rather than something that starts happening because a deployment shipped.
            //
            // MLACP-444: gateway_fee_rate (Id 1) đã gỡ. Nó mô tả phí cổng VNPay 2% nhưng không nơi
            // nào trong hệ thống tính khoản phí đó — ai đọc trang cấu hình sẽ tưởng nền tảng đang
            // trừ 2% trên mỗi giao dịch. Phí cổng hiện KHÔNG được mô hình hoá (cột Payment.GatewayFee
            // cũng chưa bao giờ được ghi). Muốn tính phí cổng thì phải làm thật ở PaymentFeeCalculator
            // rồi mới seed lại khoá này, chứ một dòng cấu hình không ai đọc không làm nên chính sách.
            new { Id = 2,  ConfigKey = "platform_commission_rate",           ConfigValue = "0.05", DataType = ConfigDataType.Decimal,  Description = "Hoa hồng nền tảng (5%) — quyết định thương mại của dự án, KHÔNG do nghị định nào quy định", UpdatedAt = seed },
            new { Id = 3,  ConfigKey = "tax_rate",                           ConfigValue = "0.05", DataType = ConfigDataType.Decimal,  Description = "Thuế GTGT khấu trừ tại nguồn (5% — tỷ lệ cho DỊCH VỤ theo NĐ 117/2025/NĐ-CP). Chỉ khấu trừ cho hộ/cá nhân kinh doanh; doanh nghiệp tự kê khai.", UpdatedAt = seed },
            new { Id = 32, ConfigKey = "personal_income_tax_rate",            ConfigValue = "0",    DataType = ConfigDataType.Decimal,  Description = "Thuế TNCN khấu trừ tại nguồn. NĐ 117/2025/NĐ-CP quy định 2% cho DỊCH VỤ của cá nhân cư trú; seed bằng 0 để việc bật khấu trừ là một quyết định vận hành có ghi lý do, không phải hệ quả của một lần triển khai. Chỉ áp cho hộ/cá nhân kinh doanh.", UpdatedAt = seed },
            // Settlement schedule — timing researched against comparable ticketing-platform payout
            // practice (Eventbrite: payout processing begins ~3 days post-event, final settlement up
            // to 14 business days for larger events) and wired into ScheduleSettlementHandler
            // 2026-08-09; previously these two keys existed but were never read by any code, and
            // their old description ("before scheduled_start"/"after actual_end") didn't match the
            // handler's actual (and better-grounded) after-show-end timing.
            new { Id = 4,  ConfigKey = "settlement_partial_hours_after_show", ConfigValue = "48",   DataType = ConfigDataType.Integer,  Description = "Số giờ sau khi buổi diễn kết thúc thì chi trả đợt đầu cho phòng trà (tỷ lệ đợt đầu tuỳ hạng phòng trà)", UpdatedAt = seed },
            new { Id = 5,  ConfigKey = "settlement_final_days_after_show",    ConfigValue = "14",   DataType = ConfigDataType.Integer,  Description = "Số ngày sau khi buổi diễn kết thúc thì chi trả phần còn lại cho phòng trà",                  UpdatedAt = seed },
            new { Id = 6,  ConfigKey = "settlement_completion_threshold_pct",ConfigValue = "0.70", DataType = ConfigDataType.Decimal,  Description = "Tỷ lệ tối thiểu giữa thời lượng diễn thực tế và theo lịch để tự động chi trả phần còn lại; thấp hơn thì khoản này chờ Admin xem xét (D16)", UpdatedAt = seed },
            // Settlement tier pre_rates (D3)
            new { Id = 7,  ConfigKey = "settlement_tier_new_pre_rate",       ConfigValue = "0.50", DataType = ConfigDataType.Decimal,  Description = "Hạng Mới: tỷ lệ chi trả đợt đầu cho phòng trà có điểm uy tín dưới 3,5 hoặc dưới 3 buổi diễn (D3)",          UpdatedAt = seed },
            new { Id = 8,  ConfigKey = "settlement_tier_standard_pre_rate",  ConfigValue = "0.70", DataType = ConfigDataType.Decimal,  Description = "Hạng Chuẩn: tỷ lệ chi trả đợt đầu cho phòng trà có điểm uy tín từ 3,5 đến 4,2 (D3)",                UpdatedAt = seed },
            new { Id = 9,  ConfigKey = "settlement_tier_premium_pre_rate",   ConfigValue = "0.80", DataType = ConfigDataType.Decimal,  Description = "Hạng Premium: tỷ lệ chi trả đợt đầu cho phòng trà có điểm uy tín từ 4,2 trở lên VÀ từ 10 buổi diễn trở lên (D3)",    UpdatedAt = seed },
            // Settlement tier thresholds
            new { Id = 10, ConfigKey = "settlement_tier_standard_min_score", ConfigValue = "3.5",  DataType = ConfigDataType.Decimal,  Description = "Điểm uy tín tối thiểu để phòng trà đạt Hạng Chuẩn (D3)",        UpdatedAt = seed },
            new { Id = 11, ConfigKey = "settlement_tier_premium_min_score",  ConfigValue = "4.2",  DataType = ConfigDataType.Decimal,  Description = "Điểm uy tín tối thiểu để phòng trà đạt Hạng Premium (D3)",       UpdatedAt = seed },
            new { Id = 12, ConfigKey = "settlement_tier_premium_min_shows",  ConfigValue = "10",   DataType = ConfigDataType.Integer,  Description = "Số buổi diễn đã hoàn tất tối thiểu để phòng trà đạt Hạng Premium (D3)",          UpdatedAt = seed },
            // Moderation (NĐ 147/2024, D11)
            //
            // MLACP-444: ai_priority_high_threshold (Id 13) và ai_priority_low_threshold (Id 14) đã
            // gỡ. Chúng mô tả việc chia hàng đợi duyệt thành "gấp"/"thường" theo điểm AI, nhưng hàng
            // đợi thật (GetPendingModerations) sắp theo AiScore giảm dần — một thang liên tục, không
            // có khái niệm nhóm nào cả. Thiết kế chia nhóm đó chưa bao giờ được xây, và sắp theo điểm
            // vốn đã làm đúng việc mà hai ngưỡng này định làm. Hai khoá anh em ai_auto_pass_threshold
            // và ai_auto_reject_threshold đã được gỡ khỏi seed từ trước vì cùng lý do.
            new { Id = 15, ConfigKey = "moderation_sla_hours",               ConfigValue = "24",   DataType = ConfigDataType.Integer,  Description = "Thời hạn (giờ) để Admin xử lý nội dung bị gắn cờ — NĐ 147/2024",               UpdatedAt = seed },
            // Tickets & Donations
            new { Id = 16, ConfigKey = "ticket_hold_minutes",                ConfigValue = "15",   DataType = ConfigDataType.Integer,  Description = "Số phút giữ chỗ trong lúc khách thanh toán; quá hạn thì nhả chỗ cho người khác (§6.3)",              UpdatedAt = seed },
            new { Id = 17, ConfigKey = "donation_hold_days",                 ConfigValue = "7",    DataType = ConfigDataType.Integer,  Description = "Số ngày, tính từ lúc phòng trà nhận tiền donate, để phòng trà chuyển cho nghệ sĩ; quá hạn mà chủ phòng trà không phản hồi thì hệ thống tự xác nhận (D4)",         UpdatedAt = seed },
            new { Id = 21, ConfigKey = "donation_performer_share_rate",      ConfigValue = "0.88", DataType = ConfigDataType.Decimal,  Description = "Tỷ lệ trên tổng tiền donate được chuyển cho nghệ sĩ (§6.5 chặng 2)",         UpdatedAt = seed },
            // Ratings & Appeals
            new { Id = 18, ConfigKey = "rating_window_days",                 ConfigValue = "7",    DataType = ConfigDataType.Integer,  Description = "Số ngày sau khi buổi diễn kết thúc mà khán giả còn được đánh giá (§6.13)",                    UpdatedAt = seed },
            new { Id = 19, ConfigKey = "appeal_sla_hours",                   ConfigValue = "48",   DataType = ConfigDataType.Integer,  Description = "Số giờ để Admin xem xét đơn kháng cáo án phạt (§6.17)",                UpdatedAt = seed },
            new { Id = 20, ConfigKey = "appeal_auto_approve",                ConfigValue = "true", DataType = ConfigDataType.Boolean,  Description = "Tự động chấp nhận đơn kháng cáo khi Admin xử lý quá hạn (§6.17)",               UpdatedAt = seed },
            // NĐ 85/2021 requires the platform be the complaint-handling focal point but doesn't
            // itself specify a numeric deadline — this is a reasonable operational target, not a
            // literal statutory figure (contrast with moderation_sla_hours, which does cite one).
            new { Id = 22, ConfigKey = "complaint_sla_hours",                ConfigValue = "72",   DataType = ConfigDataType.Integer,  Description = "Thời hạn mục tiêu (giờ) để giải quyết khiếu nại của người tiêu dùng — mục tiêu vận hành của nền tảng; NĐ 85/2021 yêu cầu nền tảng làm đầu mối nhưng không quy định số giờ",  UpdatedAt = seed },
            // Placeholder — no real Terms of Service/Privacy Policy document exists yet (2026-08-09).
            // Update this value the moment a real document is published; every new registration
            // snapshots whatever this currently says onto User.TermsVersion.
            new { Id = 23, ConfigKey = "current_terms_version",              ConfigValue = "v0-placeholder-pending-legal-review", DataType = ConfigDataType.String, Description = "Mã phiên bản Điều khoản sử dụng/Chính sách quyền riêng tư đang công bố; người đăng ký mới đồng ý theo phiên bản này — căn cứ đồng ý theo Luật 91/2025/QH15", UpdatedAt = seed },
            // Newly wired 2026-08-09: values below previously existed only as hardcoded literals in
            // handler/validator/job code (some contradicting this very table's "never hardcode"
            // convention on the line right next to them). Defaults preserve existing behavior exactly.
            new { Id = 24, ConfigKey = "publish_min_business_days_lead_time", ConfigValue = "7",        DataType = ConfigDataType.Integer, Description = "Số ngày làm việc tối thiểu từ lúc đăng hoặc dời lịch tới ngày diễn — NĐ 144/2020 Điều 10", UpdatedAt = seed },
            new { Id = 25, ConfigKey = "penalty_suspension_notice_hours",     ConfigValue = "24",       DataType = ConfigDataType.Integer, Description = "Số giờ báo trước khi án tạm đình chỉ có hiệu lực (§6.8)",     UpdatedAt = seed },
            new { Id = 26, ConfigKey = "penalty_ban_notice_days",             ConfigValue = "7",        DataType = ConfigDataType.Integer, Description = "Số ngày báo trước khi án cấm hoạt động có hiệu lực (§6.8)",            UpdatedAt = seed },
            new { Id = 27, ConfigKey = "ticket_hold_max_quantity",            ConfigValue = "10",       DataType = ConfigDataType.Integer, Description = "Số vé tối đa cho mỗi lần giữ chỗ mua online — chặn đầu cơ vé",           UpdatedAt = seed },
            new { Id = 28, ConfigKey = "walkin_ticket_max_quantity",          ConfigValue = "20",       DataType = ConfigDataType.Integer, Description = "Số vé tối đa cho mỗi lần bán tại quầy — chặn lạm dụng",    UpdatedAt = seed },
            new { Id = 29, ConfigKey = "donation_max_amount",                 ConfigValue = "50000000", DataType = ConfigDataType.Decimal, Description = "Số tiền tối đa cho một lần donate (VNĐ) — chặn gian lận",           UpdatedAt = seed },
            new { Id = 30, ConfigKey = "ticket_transfer_expiry_hours",        ConfigValue = "48",       DataType = ConfigDataType.Integer, Description = "Số giờ trước khi yêu cầu chuyển nhượng vé không được trả lời tự động bị huỷ", UpdatedAt = seed },
            // Anti-abuse rate limit on AI poster generation attempts (success+failure) per show —
            // separate from the per-Owner monthly billing quota on SubscriptionPackage, which only
            // counts successful generations.
            new { Id = 31, ConfigKey = "ai_poster_max_attempts_per_show",     ConfigValue = "5",        DataType = ConfigDataType.Integer, Description = "Số lần tạo poster AI tối đa cho mỗi buổi diễn (tính cả lần lỗi)", UpdatedAt = seed },
            new { Id = 33, ConfigKey = "ticket_last_entry_minutes",            ConfigValue = "60",       DataType = ConfigDataType.Integer, Description = "Hạn vào cửa: buổi diễn phải còn ít nhất số phút này thì mới bán vé (BR-31, giống mặc định vé không chọn chỗ của Eventbrite)", UpdatedAt = seed },
            new { Id = 34, ConfigKey = "venue_changeover_minutes",             ConfigValue = "30",       DataType = ConfigDataType.Integer, Description = "Khoảng cách tối thiểu (phút) giữa hai buổi diễn liên tiếp ở cùng phòng trà — thời gian tiễn khán giả trước và đón khán giả sau. 30 là mức thấp của khoảng 30–60 phút phổ biến ở các địa điểm hòa nhạc (CF1)", UpdatedAt = seed },
            new { Id = 35, ConfigKey = "donation_message_blocked_words",       ConfigValue = "[]",       DataType = ConfigDataType.Json,    Description = "Danh sách JSON các từ/cụm từ khiến lời nhắn donate không hiện trên thông báo livestream (so khớp nguyên từ, không phân biệt hoa thường và dấu). Khoản donate vẫn được thông báo (MLACP-360)", UpdatedAt = seed }
        );
    }
}
