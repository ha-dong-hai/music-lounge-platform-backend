namespace MusicLounge.Application.Common.Interfaces;

/// <summary>
/// MLACP-489. Ngôn ngữ mà request đang chạy yêu cầu (đọc từ <c>Accept-Language</c>).
///
/// <para><b>Quy tắc chọn ngôn ngữ của cả hệ thống, một câu:</b> thứ gì trả về <b>trong response</b> thì theo ngôn ngữ
/// của chính request đó; thứ gì gửi <b>bất đồng bộ</b> (push, email, SMS — lúc gửi không có request nào để đọc) thì
/// theo ngôn ngữ lưu trên tài khoản người nhận (<c>User.PreferredLanguage</c>).</para>
///
/// <para>Hai nguồn khác nhau là cố ý. Người bấm đổi ngôn ngữ trên giao diện phải thấy ngay danh sách thông báo đổi theo,
/// không phải chờ lưu cài đặt. Nhưng một thông báo sinh ra từ thao tác của NGƯỜI KHÁC (Admin duyệt phòng trà → báo
/// chủ phòng trà) thì ngôn ngữ của request lúc đó là của Admin, không phải của người nhận — dùng nó là gửi sai tiếng.
/// </para>
///
/// <para>Nằm ở tầng Application dưới dạng giao diện vì bộ đọc <c>Accept-Language</c> (<c>NgonNguYeuCau</c>) ở tầng Api.
/// Ngoài request HTTP (job Hangfire) thì trả tiếng Việt.</para>
/// </summary>
public interface IRequestLanguage
{
    /// <summary><c>"en"</c> hoặc <c>"vi"</c> — cùng bộ mã với <c>User.PreferredLanguage</c>.</summary>
    string Current { get; }
}
