namespace MusicLounge.Domain.ValueObjects;

/// <summary>
/// MLACP-489. Một câu gửi cho người dùng, có đủ hai thứ tiếng.
///
/// <para><b>VÌ SAO LÀ MỘT KIỂU RIÊNG MÀ KHÔNG PHẢI HAI THAM SỐ CHUỖI.</b> <c>NotifyAsync</c> có 81 chỗ gọi, nhiều chỗ
/// truyền <c>referenceType</c>/<c>referenceId</c> theo vị trí. Thêm hai tham số <c>string titleEn, string bodyEn</c>
/// vào sau <c>body</c> thì một lời gọi cũ kiểu <c>NotifyAsync(id, type, "t", "b", "show", "12")</c> vẫn biên dịch được
/// — và âm thầm đem <c>"show"</c> làm tiêu đề tiếng Anh. Đòi kiểu <see cref="SongNgu"/> thì mọi lời gọi cũ hỏng
/// biên dịch: không chỗ nào quên được bản tiếng Anh.</para>
///
/// <para><b>Vì sao không tra theo câu tiếng Việt như thông điệp lỗi API (MLACP-487).</b> Câu thông báo gần như luôn
/// có nội suy — tên buổi hòa nhạc, số tiền, mã đơn — nên chuỗi lúc chạy không bao giờ trùng chuỗi trong mã. Bản tiếng
/// Anh phải được viết ngay tại chỗ sinh ra câu, cạnh bản tiếng Việt.</para>
///
/// <para>Nội dung do người dùng tự gõ (lý do từ chối của Admin, ghi chú kháng cáo…) được chèn nguyên văn vào cả hai
/// bản — hệ thống không dịch lời của người khác.</para>
/// </summary>
public sealed record SongNgu(string Vi, string En)
{
    /// <summary>
    /// Chọn bản theo ngôn ngữ. Bản tiếng Anh rỗng thì lùi về tiếng Việt — cùng nguyên tắc với MLACP-487: một câu tiếng
    /// Việt giữa giao diện tiếng Anh thì hơi lệch, còn một ô trống thì người dùng không làm gì được với nó.
    /// </summary>
    public string Theo(string? ngonNgu) => NgonNgu.LaTiengAnh(ngonNgu) && !string.IsNullOrWhiteSpace(En) ? En : Vi;

    /// <summary>Nối hai câu song ngữ, từng thứ tiếng với nhau.</summary>
    public static SongNgu operator +(SongNgu a, SongNgu b) => new(a.Vi + b.Vi, a.En + b.En);

    /// <summary>Câu rỗng ở cả hai thứ tiếng — dùng cho nhánh "không có gì để thêm" khi ghép câu.</summary>
    public static readonly SongNgu Rong = new(string.Empty, string.Empty);
}

/// <summary>
/// Mã ngôn ngữ lưu trên tài khoản (<c>User.PreferredLanguage</c>). Chỉ hai giá trị; mặc định tiếng Việt vì đây là nền
/// tảng cho phòng trà trong nước.
/// </summary>
public static class NgonNgu
{
    public const string Viet = "vi";
    public const string Anh = "en";

    public static readonly IReadOnlyCollection<string> HopLe = [Viet, Anh];

    public static bool LaTiengAnh(string? ma) => string.Equals(ma, Anh, StringComparison.OrdinalIgnoreCase);
}
