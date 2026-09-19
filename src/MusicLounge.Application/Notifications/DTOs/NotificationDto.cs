using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Notifications.DTOs;

public sealed record NotificationDto(
    int Id,
    NotificationType Type,
    string Title,
    string Body,
    string? ReferenceType,
    string? ReferenceId,
    bool IsRead,
    DateTimeOffset CreatedAt)
{
    /// <summary>
    /// MLACP-460. Loại đối tượng bị báo cáo, tách sẵn ra khỏi <see cref="ReferenceId"/>.
    ///
    /// Cảnh báo quá hạn xử lý báo cáo vi phạm gom theo CẶP (loại, mã) nên mã tham chiếu của nó là một chuỗi ghép dạng
    /// <c>"Livestream:12"</c> — frontend muốn bấm vào thông báo để mở đúng nội dung thì phải tự cắt chuỗi, tức là đoán
    /// định dạng nội bộ của backend. Hôm nay đoán đúng; đổi dấu phân cách một lần là hỏng im lặng.
    ///
    /// Chuỗi ghép ở <see cref="ReferenceId"/> <b>giữ nguyên</b>, không đổi: nó đang là khoá chống gửi trùng của
    /// <c>ContentReportSlaBreachAlertJob</c> (job so chính chuỗi này để không cảnh báo lặp). Hai trường dưới đây chỉ là
    /// cách đọc thêm, không thay thế.
    ///
    /// <c>null</c> khi mã tham chiếu không phải dạng ghép — tức hầu hết thông báo, vốn chỉ mang một mã đơn.
    /// </summary>
    public string? ReferenceTargetType => Tach()?.Loai;

    /// <summary>Mã của đối tượng bị báo cáo. <c>null</c> cùng điều kiện với <see cref="ReferenceTargetType"/>.</summary>
    public int? ReferenceTargetId => Tach()?.Ma;

    /// <summary>
    /// Cắt <c>"Loai:Ma"</c>. Cố ý CHẶT: phần sau dấu hai chấm không phải số nguyên thì trả <c>null</c> chứ không đưa ra
    /// một mảnh chuỗi mà frontend sẽ đem đi ghép URL.
    /// </summary>
    private (string Loai, int Ma)? Tach()
    {
        if (string.IsNullOrEmpty(ReferenceId)) return null;

        var viTri = ReferenceId.IndexOf(':');
        if (viTri <= 0 || viTri == ReferenceId.Length - 1) return null;

        return int.TryParse(ReferenceId[(viTri + 1)..], out var ma)
            ? (ReferenceId[..viTri], ma)
            : null;
    }
}
