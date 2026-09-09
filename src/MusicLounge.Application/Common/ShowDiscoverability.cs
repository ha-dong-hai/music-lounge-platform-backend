using System.Linq.Expressions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common;

/// <summary>
/// Buổi diễn nào được phép xuất hiện trước mặt người đang tìm chỗ đi nghe nhạc.
///
/// Viết một lần ở đây rồi dùng cho cả truy vấn xuống database lẫn phần lọc trong bộ nhớ. Chúng là <see cref="Expression"/> chứ không phải hàm thường vì phía repository cần dịch
/// được sang SQL; phía trong bộ nhớ dùng lại đúng biểu thức đó qua
/// <c>AsQueryable().Where(...)</c>, nên không có hai bản cài đặt để lệch nhau — đúng lớp lỗi
/// MLACP-322 đã trả giá.
/// </summary>
public static class ShowDiscoverability
{
    /// <summary>
    /// Phòng trà còn đang được phép hoạt động.
    ///
    /// <b>MLACP-326.</b> Trước đây không truy vấn buổi diễn nào lọc theo trạng thái phòng trà. Cổng
    /// duyệt BR-01 (MLACP-307) chỉ chặn lúc ĐĂNG, không thu hồi những gì đã đăng — nên khi Admin
    /// đình chỉ một phòng trà, hệ thống vẫn tiếp tục chủ động đem buổi diễn của họ đi mời người
    /// dùng mua vé. Đình chỉ mà vẫn quảng bá thì việc đình chỉ chẳng có nghĩa gì.
    ///
    /// <b>Không tự định nghĩa lại "đang hoạt động".</b> Câu trả lời là
    /// <see cref="VenueLifecycle.Operating"/>, đã có từ MLACP-307 chính vì trước đó cùng một câu hỏi
    /// nhận ba đáp án khác nhau ở ba chỗ. MLACP-326 lỡ viết lại điều kiện ở đây thành bản thứ tư;
    /// MLACP-329 trỏ nó về nguồn. Cảnh cáo (<see cref="LoungeStatus.Warned"/>) nằm trong nhóm được
    /// đi qua — đó là một vết ghi lại, không phải lệnh dừng, và quyết định đó thuộc về
    /// <c>VenueLifecycle</c> chứ không phải chỗ này.
    ///
    /// <b>Phạm vi áp dụng (MLACP-329).</b> Mọi đường khám phá công khai: duyệt, tìm kiếm, gợi ý tự
    /// động điền, theo phòng trà, theo nghệ sĩ, buổi diễn tương tự, danh sách thành phố, và cả hai
    /// truy vấn ứng viên của hệ gợi ý. Cố ý KHÔNG áp cho danh sách của chính Owner, hàng đợi duyệt
    /// của Admin, và trang chi tiết buổi diễn — người đã mua vé vẫn phải tra cứu được buổi của mình.
    /// </summary>
    public static Expression<Func<LoungeShow, bool>> VenueIsOperating
        => s => VenueLifecycle.Operating.Contains(s.Lounge.Status);

    /// <summary>
    /// Buổi diễn nằm trong thành phố người dùng đang lọc.
    ///
    /// <b>Đã tra thực tế, và kết luận ngược với nghi vấn ban đầu — giữ nguyên là ĐÚNG.</b>
    ///
    /// Nghi vấn ban đầu: quy tắc này so thẳng địa chỉ phòng trà nên loại luôn buổi diễn trực tuyến
    /// của phòng trà tỉnh khác, trong khi người dùng xem được từ bất cứ đâu. Nghe như một lỗi.
    ///
    /// Nhưng chuẩn schema.org mô hình hoá đúng chỗ này: địa điểm của một buổi diễn trực tuyến là
    /// <c>VirtualLocation</c> — <i>"An online or virtual location for attending events"</i> — chứ
    /// KHÔNG phải địa chỉ của đơn vị tổ chức. Buổi trực tuyến không có thành phố; địa chỉ phòng trà
    /// chỉ là chi tiết vận hành. Buổi <see cref="LoungeShowFormat.Hybrid"/> thì mang cả hai (chuẩn
    /// cho phép vừa <c>VirtualLocation</c> vừa <c>Place</c>), nên nó khớp thành phố của chính nó là
    /// đúng.
    ///
    /// Các nền tảng thật cũng làm vậy: Meetup tách hẳn "online / in person" thành một chiều lọc
    /// RIÊNG và nói rõ bộ lọc khoảng cách chỉ dành cho sự kiện gặp mặt; Eventbrite coi "Online" là
    /// một GIÁ TRỊ của trường địa điểm chứ không phải một cờ vượt qua bộ lọc địa điểm. Không nền
    /// tảng nào trộn thẳng sự kiện trực tuyến vào kết quả lọc theo thành phố.
    ///
    /// Nên đây không phải lỗi, mà là một NĂNG LỰC CÒN THIẾU: endpoint chưa có cách nào để nói
    /// "cho tôi buổi ở thành phố này, kèm cả buổi trực tuyến". Bỏ tham số thành phố ra thì vẫn thấy
    /// hết buổi trực tuyến, nên không có gì bị giấu vĩnh viễn — chỉ là chưa gộp chung được.
    ///
    /// Muốn làm thì làm đúng cách: thêm một chiều lọc riêng theo hình thức tham dự (đúng
    /// <c>eventAttendanceMode</c> của chuẩn), mặc định giữ nguyên hành vi hiện tại, và áp đồng bộ
    /// cho cả duyệt lẫn tìm kiếm — chứ không lặng lẽ đổi nghĩa tham số thành phố ở riêng một
    /// endpoint. Đó là một tính năng có phần giao diện đi kèm, không phải một bản vá.
    /// </summary>
    public static Expression<Func<LoungeShow, bool>> ReachableFrom(string city)
        => s => s.Lounge.Address.City == city;
}
