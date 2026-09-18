namespace MusicLounge.Domain.Exceptions;

/// <summary>
/// Không tìm thấy một bản ghi — trả về 404, và <see cref="Exception.Message"/> đi thẳng vào body cho người dùng đọc.
///
/// MLACP-447: trước đây câu thông báo là <c>'{name}' với key ({key}) không tồn tại.</c>, mà <c>name</c> gần như luôn là
/// <c>nameof(...)</c> — nên người dùng thấy tên lớp C#: <c>'MusicLoungeEntity' với key (5) không tồn tại.</c> Có 269 chỗ
/// gọi như vậy. Sửa ở đây một lần thay vì sửa từng chỗ gọi: tên kỹ thuật được đổi sang nhãn tiếng Việt theo đúng thuật
/// ngữ nghiệp vụ, và tên kỹ thuật chưa có nhãn thì rơi về một câu chung — không bao giờ lộ tên lớp ra ngoài.
/// Tên kỹ thuật vẫn giữ ở <see cref="ResourceName"/> cho log và gỡ lỗi.
/// </summary>
public class NotFoundException : Exception
{
    /// <summary>Tên kỹ thuật được truyền vào (thường là <c>nameof(Entity)</c>) — chỉ dùng cho log, không hiện cho người dùng.</summary>
    public string ResourceName { get; }

    public object Key { get; }

    public NotFoundException(string name, object key)
        : base($"Không tìm thấy {NhanChoNguoiDoc(name)} (mã {key}).")
    {
        ResourceName = name;
        Key = key;
    }

    internal const string NhanChung = "dữ liệu yêu cầu";

    /// <summary>
    /// Tên kỹ thuật → nhãn tiếng Việt. Gồm mọi tên đang được truyền vào ở thời điểm viết (37 tên qua <c>nameof</c>, các chuỗi
    /// viết tay, và giá trị của <c>ReportTargetType</c>). Test <c>NotFoundMessageTests</c> quét mã nguồn để thêm entity mới
    /// mà quên nhãn thì hỏng build. Thuật ngữ: <c>LoungeShow</c> là "buổi hòa nhạc", không phải "sự kiện".
    /// </summary>
    private static readonly Dictionary<string, string> Nhan = new(StringComparer.Ordinal)
    {
        // Phòng trà và nhân sự
        ["MusicLoungeEntity"] = "phòng trà",
        ["MusicLounge"] = "phòng trà",
        ["Lounge"] = "phòng trà",
        ["LoungeGalleryImage"] = "ảnh phòng trà",
        ["LoungeStaffEntity"] = "nhân viên phòng trà",
        ["LoungeMute"] = "lựa chọn không nhận gợi ý từ phòng trà",
        ["Follow"] = "lượt theo dõi phòng trà",
        ["BusinessLicense"] = "giấy phép kinh doanh",
        ["CitizenCardImage"] = "ảnh căn cước công dân",
        ["VenuePenalty"] = "án phạt",
        ["VenueAtmosphere"] = "thẻ không khí",
        ["VenueTourScene"] = "cảnh tour 360°",
        ["VenueTourHotspot"] = "điểm tương tác trong tour 360°",
        ["VenueTourStitchAttempt"] = "lượt ghép ảnh 360°",

        // Buổi hòa nhạc
        ["LoungeShow"] = "buổi hòa nhạc",
        ["Show"] = "buổi hòa nhạc",
        ["LoungeShowRating"] = "đánh giá buổi hòa nhạc",
        ["Rating"] = "đánh giá",
        ["EventCategory"] = "danh mục buổi hòa nhạc",
        ["Mood"] = "thẻ tâm trạng",
        ["MusicGenre"] = "thể loại nhạc",
        ["Performance"] = "tiết mục",
        ["Performer"] = "nghệ sĩ",
        ["PerformerConfirmation"] = "liên kết xác nhận của nghệ sĩ",
        ["PerformerSocialLink"] = "liên kết mạng xã hội của nghệ sĩ",
        ["SeatingZone"] = "khu vực chỗ ngồi",
        ["Livestream"] = "buổi livestream",
        ["LivestreamViewingSession"] = "phiên xem livestream",
        ["EventModeration for Show"] = "yêu cầu kiểm duyệt buổi hòa nhạc",
        ["EventModeration for Livestream"] = "yêu cầu kiểm duyệt buổi livestream",
        ["EventModeration for TicketTier"] = "yêu cầu kiểm duyệt hạng vé",

        // Vé và tiền
        ["Ticket"] = "vé",
        ["TicketTier"] = "hạng vé",
        ["TicketPrice"] = "đợt bán vé",
        ["TicketHold"] = "lượt giữ vé",
        ["Payment"] = "giao dịch thanh toán",
        ["RefundRequest"] = "yêu cầu hoàn tiền",
        ["Settlement"] = "khoản quyết toán",
        ["Donation"] = "lượt donate",
        ["BankAccount"] = "tài khoản ngân hàng",
        ["SubscriptionPackage"] = "gói dịch vụ",

        // F&B
        ["FnbMenu"] = "thực đơn",
        ["FnbMenuItem"] = "món trong thực đơn",
        ["FnbOrder"] = "đơn gọi món",

        // Khác
        ["User"] = "người dùng",
        ["Notification"] = "thông báo",
        ["Complaint"] = "khiếu nại",
        ["CustomCriteriaEntity"] = "tiêu chí tuỳ chỉnh",
        ["SystemConfig"] = "khoá cấu hình",
        ["Wishlist entry"] = "mục trong danh sách yêu thích",
    };

    /// <summary>
    /// Nhãn người đọc được cho <paramref name="name"/>. Chuỗi đã là tiếng Việt (có ký tự ngoài ASCII) thì giữ nguyên —
    /// vài chỗ gọi đã viết sẵn câu cho người đọc. Còn lại, không có nhãn thì dùng câu chung chứ không trả lại tên kỹ thuật.
    /// Không suy "có dấu cách là câu cho người đọc": <c>"EventModeration for Show"</c> có dấu cách mà vẫn là tên kỹ thuật.
    /// </summary>
    internal static string NhanChoNguoiDoc(string name)
    {
        if (Nhan.TryGetValue(name, out var nhan)) return nhan;
        if (name.Any(c => c > 127)) return char.ToLowerInvariant(name[0]) + name[1..];
        return NhanChung;
    }
}
