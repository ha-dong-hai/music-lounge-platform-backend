namespace MusicLounge.Domain.Enums;

/// <summary>
/// MLACP-676. Loại lý do chủ phòng trà chọn khi huỷ một buổi hòa nhạc đã mở bán. Admin dựa vào loại + mô tả + bằng chứng để
/// quyết miễn phạt hay phạt; khán giả chỉ thấy câu trung tính của từng loại (xem ShowCancellationReasons).
/// </summary>
public enum ShowCancellationReason
{
    /// <summary>Bất khả kháng: thiên tai, dịch bệnh, sự cố an ninh — ngoài khả năng kiểm soát của phòng trà.</summary>
    ForceMajeure,
    /// <summary>Nghệ sĩ không thể biểu diễn (ốm, tai nạn, việc gia đình khẩn cấp) và không có người thay.</summary>
    PerformerUnavailable,
    /// <summary>Cơ quan nhà nước có thẩm quyền yêu cầu dừng hoặc không cấp phép.</summary>
    AuthorityRequest,
    /// <summary>Sự cố tại phòng trà: mất điện, hỏng âm thanh, hư hại cơ sở vật chất.</summary>
    VenueIncident,
    /// <summary>Bán được ít vé.</summary>
    LowSales,
    Other
}
