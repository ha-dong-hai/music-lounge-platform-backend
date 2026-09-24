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
        ["ChatMessage"] = "tin nhắn chat",
        ["LivestreamChatMessage"] = "tin nhắn chat",
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
        ["AiPosterGeneration"] = "đơn tạo poster",
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

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // MLACP-487 — bản tiếng Anh, đặt NGAY CẠNH bản tiếng Việt chứ không để ở tầng Api.
    //
    // Yêu cầu phi chức năng đã đăng ký trong văn bản đề tài (05/04/2026): "Bilingual interface:
    // Vietnamese & English."
    //
    // VÌ SAO ĐẶT Ở ĐÂY, dù localization thường thuộc tầng ngoài: hai từ điển phải có CÙNG BỘ KHOÁ,
    // và cách rẻ nhất để giữ điều đó là để chúng cạnh nhau — thêm một thực thể mà quên nhãn thì
    // người sửa nhìn thấy cả hai chỗ trong cùng một màn hình. Tách sang assembly khác thì `Nhan`
    // (private) không đọc được từ bên ngoài, nên bộ khoá sẽ phải chép tay và sẽ trôi ra khỏi nhau.
    // Tầng Api chỉ gọi CauTiengAnh(), không tự dựng câu.
    //
    // Câu tiếng Việt do constructor dựng, nên tầng Api KHÔNG bóc tách chuỗi đã ghép — nó dựng lại
    // từ ResourceName + Key vốn đã phơi sẵn. Bóc chuỗi bằng regex sẽ vỡ ngay lần đầu ai sửa dấu câu.
    // ─────────────────────────────────────────────────────────────────────────────────────────

    internal const string NhanChungEn = "the requested record";

    private static readonly Dictionary<string, string> NhanEn = new(StringComparer.Ordinal)
    {
        // Phòng trà và nhân sự
        ["MusicLoungeEntity"] = "music lounge",
        ["MusicLounge"] = "music lounge",
        ["Lounge"] = "music lounge",
        ["LoungeGalleryImage"] = "lounge photo",
        ["LoungeStaffEntity"] = "lounge staff member",
        ["LoungeMute"] = "the choice to stop receiving suggestions from this lounge",
        ["Follow"] = "lounge follow",
        ["BusinessLicense"] = "business licence",
        ["CitizenCardImage"] = "ID card image",
        ["VenuePenalty"] = "penalty",
        ["VenueAtmosphere"] = "atmosphere tag",
        ["VenueTourScene"] = "360° tour scene",
        ["VenueTourHotspot"] = "360° tour hotspot",
        ["VenueTourStitchAttempt"] = "360° stitching attempt",
        // Buổi hòa nhạc
        ["LoungeShow"] = "concert",
        ["Show"] = "concert",
        ["LoungeShowRating"] = "concert rating",
        ["Rating"] = "rating",
        ["ChatMessage"] = "chat message",
        ["LivestreamChatMessage"] = "chat message",
        ["EventCategory"] = "concert category",
        ["Mood"] = "mood tag",
        ["MusicGenre"] = "music genre",
        ["Performance"] = "performance",
        ["Performer"] = "performer",
        ["PerformerConfirmation"] = "performer confirmation link",
        ["PerformerSocialLink"] = "performer social link",
        ["SeatingZone"] = "seating zone",
        ["Livestream"] = "livestream",
        ["LivestreamViewingSession"] = "livestream viewing session",
        ["EventModeration for Show"] = "concert moderation request",
        ["EventModeration for Livestream"] = "livestream moderation request",
        ["EventModeration for TicketTier"] = "ticket tier moderation request",
        // Vé và tiền
        ["Ticket"] = "ticket",
        ["TicketTier"] = "ticket tier",
        ["TicketPrice"] = "ticket sale phase",
        ["TicketHold"] = "ticket hold",
        ["Payment"] = "payment",
        ["RefundRequest"] = "refund request",
        ["Settlement"] = "settlement",
        ["Donation"] = "donation",
        ["BankAccount"] = "bank account",
        ["SubscriptionPackage"] = "subscription package",
        // F&B
        ["FnbMenu"] = "menu",
        ["FnbMenuItem"] = "menu item",
        ["FnbOrder"] = "food and drink order",
        // Khác
        ["AiPosterGeneration"] = "poster generation job",
        ["User"] = "user",
        ["Notification"] = "notification",
        ["Complaint"] = "complaint",
        ["CustomCriteriaEntity"] = "custom criterion",
        ["SystemConfig"] = "configuration key",
        ["Wishlist entry"] = "wishlist entry",
    };

    /// <summary>
    /// Câu 404 bằng tiếng Anh, dựng lại từ <paramref name="name"/> và <paramref name="key"/>.
    ///
    /// <para>Tên kỹ thuật chưa có nhãn tiếng Anh thì rơi về <see cref="NhanChungEn"/> — KHÔNG bao
    /// giờ lộ tên lớp C# ra ngoài, đúng lý do MLACP-447 đã sửa cho bản tiếng Việt.</para>
    ///
    /// <para>Chuỗi truyền vào vốn đã là tiếng Việt (vài chỗ gọi viết sẵn câu cho người đọc) thì
    /// KHÔNG dịch được ở đây — trả nhãn chung, vì trả lại tiếng Việt giữa một bản tiếng Anh còn khó
    /// hiểu hơn.</para>
    /// </summary>
    public static string CauTiengAnh(string name, object key)
    {
        var nhan = NhanEn.TryGetValue(name, out var n) ? n : NhanChungEn;
        return $"Could not find {nhan} (id {key}).";
    }
}
